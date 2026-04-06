// src/auth.js

const STORAGE_KEY = 'RemotePC_Auth_v1';
const DEFAULT_TTL_MS = 1000 * 60 * 60; // 1 hour
const LOGIN_API_URL = 'https://web1.remotepc.com/rpcnew/api/login/v5/validateLogin';
const RPC_AESKey256 = "waxOcALDCOcAk1U0QW05R0TwKw5T7XpT";
const PRODUCT_CODE = 'rpc';
let _authCache = null;


const RECAPTCHA_SITE_KEY = '6LfQEJ8sAAAAAPYqRLO37VdQi3nyxReThi0fkalv';
const RECAPTCHA_PARAM_NAME = 'captcha_token';

/* ---------- Storage Helpers ---------- */
function _generateLocalToken() {
    const array = new Uint8Array(16);
    crypto.getRandomValues(array);
    return Array.from(array).map(b => b.toString(16).padStart(2, '0')).join('');
}

function _storeAuth({ username, token, expiresAt, userData }) {
    const obj = { username, token, expiresAt, userData };
    localStorage.setItem(STORAGE_KEY, JSON.stringify(obj));
    _authCache = obj;
}

function _readAuth() {
    if (_authCache) {
        if (_authCache.expiresAt > Date.now()) return _authCache;
        _authCache = null;
        localStorage.removeItem(STORAGE_KEY);
        return null;
    }
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    try {
        const obj = JSON.parse(raw);
        if (obj.expiresAt && obj.expiresAt > Date.now()) {
            _authCache = obj;
            return obj;
        } else {
            localStorage.removeItem(STORAGE_KEY);
            return null;
        }
    } catch (e) {
        localStorage.removeItem(STORAGE_KEY);
        return null;
    }
}

function _makeMachineId() {
    const bytes = new Uint8Array(6);
    crypto.getRandomValues(bytes);
    return Array.from(bytes).map(b => b.toString(16).padStart(2, '0')).join('').toUpperCase();
}

/* ---------- Encryption Helpers ---------- */
function _getAESIV256(macId) {
    if (macId.length < 16) {
        return macId.padEnd(16, '0');
    }
    return macId.substring(0, 16);
}

function _encodeStringRPC(plainText) {
    const KEY_NO = 4;
    let iHashCount = KEY_NO - (plainText.length % KEY_NO);
    let sHashed = plainText;

    if (iHashCount >= KEY_NO) {
        iHashCount = 0;
    } else {
        sHashed += '#'.repeat(iHashCount);
    }

    let iLengthCount = 0;
    if (plainText.length > 99) {
        iLengthCount = 7;
    } else if (plainText.length > 9) {
        iLengthCount = 8;
    } else {
        iLengthCount = 9;
    }
    let sNoOfZeros = '0'.repeat(iLengthCount);

    let sSwapped = '';
    let index = 0;
    while (index < sHashed.length) {
        let chunk = sHashed.substring(index, index + KEY_NO);
        if (chunk.length > 1) {
            chunk = chunk.substring(chunk.length - 1) + chunk.substring(0, chunk.length - 1);
        }
        sSwapped += chunk;
        index += KEY_NO;
    }

    const combined = iHashCount.toString() + sNoOfZeros + plainText.length.toString() + sSwapped;

    const utf8Bytes = new TextEncoder().encode(combined);
    let binary = '';
    for (let i = 0; i < utf8Bytes.byteLength; i++) {
        binary += String.fromCharCode(utf8Bytes[i]);
    }
    return btoa(binary);
}

async function _encryptRPC(plainText, macId) {
    const ivString = _getAESIV256(macId);
    const encodedBase64Str = _encodeStringRPC(plainText);

    const encoder = new TextEncoder();
    const keyData = encoder.encode(RPC_AESKey256);
    const ivData = encoder.encode(ivString);
    const textData = encoder.encode(encodedBase64Str);

    const cryptoKey = await crypto.subtle.importKey(
        "raw",
        keyData,
        { name: "AES-CBC" },
        false,
        ["encrypt"]
    );

    const encryptedBuffer = await crypto.subtle.encrypt(
        { name: "AES-CBC", iv: ivData },
        cryptoKey,
        textData
    );

    const encryptedBytes = new Uint8Array(encryptedBuffer);
    let binary = '';
    for (let i = 0; i < encryptedBytes.byteLength; i++) {
        binary += String.fromCharCode(encryptedBytes[i]);
    }
    return btoa(binary);
}

/* ---------- API Logic ---------- */
async function _validateLoginWithApi({ username, password, rememberMe }) {
    const machineId = _makeMachineId();

    const encryptedUsername = await _encryptRPC(username, machineId);
    const encryptedPassword = await _encryptRPC(password, machineId);

    // --- reCAPTCHA Placeholder ---
   console.log('AUTH: Requesting invisible reCAPTCHA token...');
    let captchaToken = '';
    try {
        captchaToken = await _getRecaptchaToken();
        console.log('AUTH: Got reCAPTCHA token successfully.');
    } catch (err) {
        console.warn('AUTH: reCAPTCHA error', err);
        throw new Error('Security check failed. Please check your connection and try again.'); 
    }

    const params = new URLSearchParams();
    params.set('username', encryptedUsername);
    params.set('password', encryptedPassword);
    params.set('id', machineId);
    params.set('os', '1');
    params.set('verify_2fa', '0');
    params.set('device_name', 'Meet Add-on'); 
    params.set('passcode', '');
    params.set('productCode', PRODUCT_CODE);
    params.set(RECAPTCHA_PARAM_NAME, captchaToken); 

    const requestUrl = `${LOGIN_API_URL}?${params.toString()}`;

    const response = await fetch(requestUrl, {
        method: 'POST',
        headers: {
            'Accept': 'application/json'
        }
    });

    let payload = null;
    try { payload = await response.json(); } catch (e) { payload = null; }

    if (!response.ok) {
        let apiMessage = `HTTP ${response.status}`;
        if (payload?.errors && payload.errors.length > 0) {
            apiMessage = payload.errors[0].description;
        } else if (payload?.message) {
            apiMessage = payload.message;
        } else if (payload?.error) {
            apiMessage = payload.error;
        }
        throw new Error(apiMessage);
    }

    const status = String(payload?.status || '').toUpperCase();
    const isSuccess = status === 'OK' || status === 'SUCCESS' || payload?.success === true;

    if (!isSuccess) {
        let apiMessage = 'Invalid username or password';
        if (payload?.errors && payload.errors.length > 0) {
            apiMessage = payload.errors[0].description;
        }
        throw new Error(apiMessage);
    }

    let parsedUserData = payload;
    if (typeof payload?.message === 'string') {
        try {
            parsedUserData = JSON.parse(payload.message);
        } catch (e) {
            console.warn('AUTH: Failed to parse message string from API');
        }
    } else if (payload?.message) {
        parsedUserData = payload.message;
    }

    const tokenFromApi = payload?.message?.access_token || payload?.access_token || payload?.token || _generateLocalToken();
    const ttl = rememberMe ? DEFAULT_TTL_MS * 24 : DEFAULT_TTL_MS;

    return {
        token: tokenFromApi,
        expiresAt: Date.now() + ttl,
        userData: parsedUserData
    };
}

/* ---------- Modal Creation & UI ---------- */
function _createModalIfNeeded() {
    let modal = document.getElementById('rpc-login-modal');
    if (modal) return modal;

    modal = document.createElement('div');
    modal.id = 'rpc-login-modal';
    modal.setAttribute('role', 'dialog');
    modal.setAttribute('aria-modal', 'true');
    modal.style.position = 'fixed';
    modal.style.inset = '0';
    modal.style.zIndex = '99999';
    modal.style.display = 'flex';
    modal.style.alignItems = 'center';
    modal.style.justifyContent = 'center';
    modal.style.background = 'rgba(0,0,0,0.36)';
    modal.style.backdropFilter = 'blur(2px)';
    modal.style.padding = '16px'; // Reduced padding

    modal.innerHTML = `
    <!DOCTYPE html>
    <html lang="en">
    <head>
        <meta charset="UTF-8">
        <meta name="viewport" content="width=device-width, initial-scale=1.0">
        <title>RemotePC Login</title>
        <style>
          :root {
            --rpc-bg: #ffffff;
            --rpc-muted: #5f6368;
            --rpc-primary: #1a73e8;
            --rpc-radius: 12px;
            --rpc-shadow: 0 12px 40px rgba(13, 37, 63, 0.18);
            --rpc-gap: 12px;
            --rpc-input-border: #e6e6e6;
          }

          body {
            display: grid;
            place-items: center;
            min-height: 100vh;
            margin: 0;
            background-color: transparent;
          }

          #rpc-login-card, #rpc-login-card * { box-sizing: border-box; }

          #rpc-login-card {
            width: 440px; /* Slightly narrower */
            max-width: calc(100vw - 32px);
            border-radius: var(--rpc-radius);
            background: var(--rpc-bg);
            box-shadow: var(--rpc-shadow);
            padding: 20px; /* Tighter padding */
            font-family: "Google Sans", Roboto, Arial, sans-serif;
            animation: rpc-zoom-in .18s cubic-bezier(.2,.9,.3,1);
            transform-origin: center;
            display: flex;
            flex-direction: column;
            gap: 12px;
          }
          @keyframes rpc-zoom-in { from { transform: translateY(8px) scale(.98); opacity:0 } to { transform: translateY(0) scale(1); opacity:1 } }

          .rpc-header {
            display:flex;
            gap: 12px;
            align-items:center;
          }
          .rpc-logo {
            width:48px; /* Slightly smaller logo */
            height:48px;
            border-radius:10px;
            object-fit:contain;
            background:#f7f7f7;
            flex:0 0 48px;
          }
          .rpc-title { font-size:18px; color:#202124; font-weight:600; }
          .rpc-sub { font-size:13px; color:var(--rpc-muted); margin-top:2px; }

          .rpc-form {
            display:flex;
            flex-direction:column;
            gap:12px;
          }

          .rpc-field { display:flex; flex-direction:column; gap:4px; } /* Tighter gap */
          .rpc-label { font-size:13px; color:var(--rpc-muted); margin-left:4px; }

          .rpc-input {
            width:100%;
            padding: 10px 40px 10px 12px; /* Tighter input height */
            font-size:14px;
            border-radius:8px;
            border:1px solid #9aa0a6;
            background:#fff;
            outline:none;
            transition: box-shadow .12s ease, border-color .12s ease;
          }
          .rpc-input:focus {
            box-shadow: 0 8px 26px rgba(26,115,232,0.12);
            border-color: rgba(26,115,232,0.9);
          }

          .rpc-password-wrapper { position: relative; }
          .rpc-password-toggle {
            position: absolute;
            right: 6px;
            top: 50%;
            transform: translateY(-50%);
            display: flex;
            align-items: center;
            justify-content: center;
            min-width: 32px;
            padding: 4px;
            border-radius: 8px;
            border: none;
            background: transparent;
            cursor: pointer;
            color: var(--rpc-muted);
          }
          .rpc-password-toggle svg { display:block; width:18px; height:18px; } /* Slightly smaller eye */

          .rpc-aux { display:flex; justify-content:space-between; align-items:center; }
          .rpc-remember { display:flex; align-items:center; gap:6px; color:var(--rpc-muted); font-size:13px; }
          .rpc-forgot { background:transparent; border:none; color:var(--rpc-primary); font-size:13px; cursor:pointer; padding:4px; }

          .rpc-actions { display:flex; justify-content:flex-end; gap:8px; align-items:center; }
          .rpc-btn { padding:8px 16px; border-radius:8px; border:none; font-size:14px; cursor:pointer; font-weight:500; }
          .rpc-btn-primary { background:var(--rpc-primary); color:#fff; }
          .rpc-btn-ghost { background:transparent; color:var(--rpc-primary); }

          /* Centered error text */
          .rpc-error { 
            color:#b00020; 
            font-size:13px; 
            text-align: center; 
            min-height: 16px; 
          }

          .rpc-loading { display:inline-block; width:14px; height:14px; border-radius:50%; border:2px solid rgba(255,255,255,0.25); border-top-color:rgba(255,255,255,0.95); animation:rpc-spin .9s linear infinite; }
          @keyframes rpc-spin { to { transform: rotate(360deg) } }
        </style>
    </head>
    <body>

    <div id="rpc-login-card" role="document" aria-labelledby="rpc-title">
      <div class="rpc-header">
        <img src="./logo.png" class="rpc-logo" alt="RemotePC"/>
        <div>
          <div id="rpc-title" class="rpc-title">RemotePC</div>
          <div class="rpc-sub">Sign in to start or join a session</div>
        </div>
      </div>

      <form id="rpc-form" class="rpc-form" autocomplete="on" novalidate>
        <div class="rpc-field">
          <label class="rpc-label" for="rpc-login-username">Username</label>
          <input id="rpc-login-username" class="rpc-input" name="username" autocomplete="username" />
        </div>

        <div class="rpc-field">
          <label class="rpc-label" for="rpc-login-password">Password</label>
          <div class="rpc-password-wrapper">
            <input id="rpc-login-password" class="rpc-input" name="password" type="password" autocomplete="current-password" />
            <button id="rpc-password-toggle" type="button" class="rpc-password-toggle" aria-pressed="false" title="Show password">
              <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
                <path d="M12 5c-7 0-10 7-10 7s3 7 10 7 10-7 10-7-3-7-10-7z" stroke="#5f6368" stroke-width="1.5" fill="none"></path>
                <circle cx="12" cy="12" r="3" stroke="#5f6368" stroke-width="1.5" fill="none"></circle>
              </svg>
            </button>
          </div>
        </div>

        <div class="rpc-aux">
          <label class="rpc-remember"><input id="rpc-remember" type="checkbox" /> Remember me</label>
          <button id="rpc-forgot" type="button" class="rpc-forgot">Forgot?</button>
        </div>

        <div id="rpc-error" class="rpc-error" role="alert" aria-live="assertive" style="display:none"></div>

        <div class="rpc-actions">
          <button id="rpc-cancel" type="button" class="rpc-btn rpc-btn-ghost">Cancel</button>
          <button id="rpc-submit" type="submit" class="rpc-btn rpc-btn-primary">Sign in</button>
        </div>
      </form>
    </div>

    </body>
    </html>
  `;

    document.body.appendChild(modal);
    return modal;
}

function _preventBodyScroll(prevent) {
    if (prevent) {
        document.documentElement.style.overflow = 'hidden';
        document.body.style.overflow = 'hidden';
    } else {
        document.documentElement.style.overflow = '';
        document.body.style.overflow = '';
    }
}

function _trapFocus(modalEl) {
    const focusableSelector = 'a[href], button:not([disabled]), textarea, input, select, [tabindex]:not([tabindex="-1"])';
    const first = modalEl.querySelector(focusableSelector);
    const focusables = Array.from(modalEl.querySelectorAll(focusableSelector));
    let last = focusables[focusables.length - 1];
    let handler = function (e) {
        if (e.key !== 'Tab') return;
        if (focusables.length === 0) { e.preventDefault(); return; }
        if (e.shiftKey) {
            if (document.activeElement === first) { e.preventDefault(); last.focus(); }
        } else {
            if (document.activeElement === last) { e.preventDefault(); first.focus(); }
        }
    };
    document.addEventListener('keydown', handler);
    return () => document.removeEventListener('keydown', handler);
}

/* ---------- Public API: getAuthUser(mode) ---------- */
export function getAuthUser(mode = 'silent') {
    const cached = _readAuth();
    if (cached) return Promise.resolve(cached.username);

    if (mode !== 'prompt') return Promise.reject(new Error('No cached credentials'));

    return new Promise((resolve, reject) => {
        const modal = _createModalIfNeeded();
        modal.style.display = 'flex';
        _preventBodyScroll(true);

        const form = document.getElementById('rpc-form');
        const usernameEl = document.getElementById('rpc-login-username');
        const passwordEl = document.getElementById('rpc-login-password');
        const submitBtn = document.getElementById('rpc-submit');
        const cancelBtn = document.getElementById('rpc-cancel');
        const errorEl = document.getElementById('rpc-error');
        const toggleBtn = document.getElementById('rpc-password-toggle');
        const rememberEl = document.getElementById('rpc-remember');
        const forgotBtn = document.getElementById('rpc-forgot');

        // Initial UI Reset
        errorEl.style.display = 'none';
        errorEl.textContent = '';
        usernameEl.value = '';
        passwordEl.value = '';
        usernameEl.focus();

        let destroyed = false;
        let errorTimer = null; // Timer for auto-vanishing errors
        const focusTeardown = _trapFocus(modal);

        function teardown() {
            if (destroyed) return;
            destroyed = true;
            if (errorTimer) clearTimeout(errorTimer);
            _preventBodyScroll(false);
            modal.style.display = 'none';
            form.removeEventListener('submit', onSubmit);
            cancelBtn.removeEventListener('click', onCancel);
            toggleBtn.removeEventListener('click', onToggle);
            forgotBtn.removeEventListener('click', onForgot);
            document.removeEventListener('keydown', onKeyDown);
            focusTeardown();
        }

        function setLoading(loading) {
            submitBtn.disabled = loading;
            cancelBtn.disabled = loading;
            submitBtn.innerHTML = loading ? '<span class="rpc-loading" aria-hidden="true"></span>' : 'Sign in';
        }

        // --- NEW: Helper to show, center, and vanish errors ---
        function showError(msg) {
            errorEl.textContent = msg;
            errorEl.style.display = 'block';
            setLoading(false);

            if (errorTimer) clearTimeout(errorTimer);
            errorTimer = setTimeout(() => {
                errorEl.style.display = 'none';
                errorEl.textContent = '';
            }, 5000); // Vanish after 5 seconds
        }

        function onToggle() {
            const shown = passwordEl.type === 'text';
            passwordEl.type = shown ? 'password' : 'text';
            toggleBtn.setAttribute('aria-pressed', String(!shown));
            passwordEl.focus();
        }

        function onForgot(e) {
            e.preventDefault();
            window.open("https://app.remotepc.com/rpcnew/forgotPassword", "_blank");
        }

        function onCancel(e) {
            if (e) e.preventDefault();
            teardown();
            reject(new Error('User cancelled login'));
        }

        function onKeyDown(e) {
            if (e.key === 'Escape') onCancel();
        }

        async function onSubmit(e) {
            if (e) e.preventDefault();
            errorEl.style.display = 'none';
            if (errorTimer) clearTimeout(errorTimer);

            const username = usernameEl.value.trim();
            const password = passwordEl.value;

            if (!username || !password) {
                showError('Please enter username and password');
                return;
            }

            setLoading(true);

            try {
                const { token, expiresAt, userData } = await _validateLoginWithApi({
                    username,
                    password,
                    rememberMe: rememberEl.checked
                });
                _storeAuth({ username, token, expiresAt, userData });
                teardown();
                resolve(username);
            } catch (err) {
                console.error('Login failed', err);
                showError(err.message || 'Sign-in failed');
            }
        }

        form.addEventListener('submit', onSubmit);
        cancelBtn.addEventListener('click', onCancel);
        toggleBtn.addEventListener('click', onToggle);
        forgotBtn.addEventListener('click', onForgot);
        document.addEventListener('keydown', onKeyDown);
    });
}

/* ---------- logout ---------- */
export async function logout() {
    try {
        localStorage.removeItem(STORAGE_KEY);
        _authCache = null;
    } catch (e) {
        console.warn('AUTH: logout() cleanup error', e);
    }
}


function _getRecaptchaToken() {
    return new Promise((resolve, reject) => {
        if (!document.getElementById('recaptcha-script')) {
            const script = document.createElement('script');
            script.id = 'recaptcha-script';
            script.src = `https://www.google.com/recaptcha/api.js?render=${RECAPTCHA_SITE_KEY}`;
            document.head.appendChild(script);
        }

        const checkReady = setInterval(() => {
            if (window.grecaptcha && window.grecaptcha.ready) {
                clearInterval(checkReady);
                window.grecaptcha.ready(() => {
                    window.grecaptcha.execute(RECAPTCHA_SITE_KEY, { action: 'login' })
                        .then((token) => resolve(token))
                        .catch((err) => reject(err));
                });
            }
        }, 100);

        setTimeout(() => {
            clearInterval(checkReady);
            reject(new Error('reCAPTCHA failed to load.'));
        }, 5000);
    });
}
