// src/auth.js
// Polished client-side auth modal for RemotePC (dev stub).
// - Keeps the same API: getAuthUser(mode) and logout()
// - Caches username/token in localStorage (dev). Replace with API later.

const STORAGE_KEY = 'RemotePC_Auth_v1';
const DEFAULT_TTL_MS = 1000 * 60 * 60; // 1 hour
let _authCache = null;

/* ---------- storage helpers ---------- */
function _generateToken() {
    const array = new Uint8Array(16);
    crypto.getRandomValues(array);
    return Array.from(array).map(b => b.toString(16).padStart(2, '0')).join('');
}

function _storeAuth({ username, token, expiresAt }) {
    const obj = { username, token, expiresAt };
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

/* ---------- modal creation & improved UI ---------- */

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
    modal.style.padding = '20px';

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
            background-color: #f0f2f5;
          }

          #rpc-login-card, #rpc-login-card * { box-sizing: border-box; }

          #rpc-login-card {
            width: 460px;
            max-width: calc(100vw - 32px);
            border-radius: var(--rpc-radius);
            background: var(--rpc-bg);
            box-shadow: var(--rpc-shadow);
            padding: 24px;
            font-family: "Google Sans", Roboto, Arial, sans-serif;
            animation: rpc-zoom-in .18s cubic-bezier(.2,.9,.3,1);
            transform-origin: center;
            display: flex;
            flex-direction: column;
            gap: 10px;
          }
          @keyframes rpc-zoom-in { from { transform: translateY(8px) scale(.98); opacity:0 } to { transform: translateY(0) scale(1); opacity:1 } }

          .rpc-header {
            display:flex;
            gap: 12px;
            align-items:center;
            margin-bottom: 4px;
          }
          .rpc-logo {
            width:56px;
            height:56px;
            border-radius:10px;
            object-fit:contain;
            background:#f7f7f7;
            flex:0 0 56px;
          }
          .rpc-title { font-size:18px; color:#202124; font-weight:600; }
          .rpc-sub { font-size:13px; color:var(--rpc-muted); margin-top:4px; }

          .rpc-form {
            margin-top: 8px;
            display:flex;
            flex-direction:column;
            gap:12px;
            max-height: calc(100vh - 180px);
            overflow:auto;
            padding-right: 6px;
          }

          .rpc-field { display:flex; flex-direction:column; gap:6px; }
          .rpc-label { font-size:13px; color:var(--rpc-muted); margin-left:4px; }

          .rpc-input {
            width:100%;
            padding: 12px 44px 12px 12px;
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

          /* --- CORRECTION --- */
          /* This new wrapper div now acts as the positioning container. */
          .rpc-password-wrapper {
            position: relative;
          }

          .rpc-password-toggle {
            position: absolute;
            right: 8px;
            top: 50%;
            transform: translateY(-50%);
            display: flex;
            align-items: center;
            justify-content: center;
            min-width: 36px;
            padding: 4px 6px;
            border-radius: 8px;
            border: none;
            background: transparent;
            cursor: pointer;
            color: var(--rpc-muted);
            font-size: 13px;
          }

          .rpc-password-toggle svg { display:block; width:21px; height:21px; }

          .rpc-aux { display:flex; justify-content:space-between; align-items:center; margin-top:4px; gap:8px; }
          .rpc-remember { display:flex; align-items:center; gap:8px; color:var(--rpc-muted); font-size:13px; }
          .rpc-forgot { background:transparent; border:none; color:var(--rpc-primary); font-size:13px; cursor:pointer; padding:6px; }

          .rpc-actions { display:flex; justify-content:flex-end; gap:10px; margin-top:6px; align-items:center; }
          .rpc-btn { padding:10px 14px; border-radius:10px; border:none; font-size:14px; cursor:pointer; }
          .rpc-btn-primary { background:var(--rpc-primary); color:#fff; }
          .rpc-btn-ghost { background:transparent; color:var(--rpc-primary); }

          .rpc-error { color:#b00020; font-size:13px; margin-top:4px; }

          .rpc-loading { display:inline-block; width:16px; height:16px; border-radius:50%; border:3px solid rgba(255,255,255,0.25); border-top-color:rgba(255,255,255,0.95); animation:rpc-spin .9s linear infinite; }
          @keyframes rpc-spin { to { transform: rotate(360deg) } }

          @media (max-width:480px) {
            #rpc-login-card { width:100%; padding:16px; }
            .rpc-logo { width:48px; height:48px; flex:0 0 48px; }
          }
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

        <!-- --- CORRECTION --- -->
        <!-- The outer div is just for flex layout now. -->
        <div class="rpc-field">
          <label class="rpc-label" for="rpc-login-password">Password</label>
          <!-- This new wrapper contains the input and button. -->
          <div class="rpc-password-wrapper">
            <input id="rpc-login-password" class="rpc-input" name="password" type="password" autocomplete="current-password" />
            <button id="rpc-password-toggle" type="button" class="rpc-password-toggle" aria-pressed="false" title="Show password">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                <path d="M12 5c-7 0-10 7-10 7s3 7 10 7 10-7 10-7-3-7-10-7z" stroke="#5f6368" stroke-width="1.2" fill="none"></path>
                <circle cx="12" cy="12" r="3" stroke="#5f6368" stroke-width="1.2" fill="none"></circle>
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
          <button id="rpc-cancel" type="button" class="rpc-btn">Cancel</button>
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


/* ---------- helpers to open/close modal & trap focus ---------- */

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
    // Ensure focus stays within modal; returns a teardown function
    const focusableSelector = 'a[href], button:not([disabled]), textarea, input, select, [tabindex]:not([tabindex="-1"])';
    const first = modalEl.querySelector(focusableSelector);
    const focusables = Array.from(modalEl.querySelectorAll(focusableSelector));
    let last = focusables[focusables.length - 1];
    let handler = function (e) {
        if (e.key !== 'Tab') return;
        if (focusables.length === 0) {
            e.preventDefault();
            return;
        }
        if (e.shiftKey) {
            if (document.activeElement === first) {
                e.preventDefault();
                last.focus();
            }
        } else {
            if (document.activeElement === last) {
                e.preventDefault();
                first.focus();
            }
        }
    };
    document.addEventListener('keydown', handler);
    // return cleanup
    return () => document.removeEventListener('keydown', handler);
}

/* ---------- Public API: getAuthUser(mode) ---------- */
/**
 * mode: 'silent' | 'prompt'
 * - silent: return cached username or reject
 * - prompt: open modal if needed
 */
export function getAuthUser(mode = 'silent') {
    const cached = _readAuth();
    if (cached) return Promise.resolve(cached.username);

    if (mode !== 'prompt') return Promise.reject(new Error('No cached credentials'));

    return new Promise((resolve, reject) => {
        const modal = _createModalIfNeeded();
        modal.style.display = 'flex';
        _preventBodyScroll(true);

        // refs
        const form = document.getElementById('rpc-form');
        const usernameEl = document.getElementById('rpc-login-username');
        const passwordEl = document.getElementById('rpc-login-password');
        const submitBtn = document.getElementById('rpc-submit');
        const cancelBtn = document.getElementById('rpc-cancel');
        const errorEl = document.getElementById('rpc-error');
        const toggleBtn = document.getElementById('rpc-password-toggle');
        const rememberEl = document.getElementById('rpc-remember');
        const forgotBtn = document.getElementById('rpc-forgot');

        // initial UI
        errorEl.style.display = 'none';
        errorEl.textContent = '';
        usernameEl.value = '';
        passwordEl.value = '';
        usernameEl.focus();

        let destroyed = false;
        const focusTeardown = _trapFocus(modal);

        function teardown() {
            if (destroyed) return;
            destroyed = true;
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
            if (loading) {
                submitBtn.innerHTML = '<span class="rpc-loading" aria-hidden="true"></span>';
            } else {
                submitBtn.innerHTML = 'Sign in';
            }
        }

        function onToggle(e) {
            const shown = passwordEl.type === 'text';
            passwordEl.type = shown ? 'password' : 'text';
            toggleBtn.setAttribute('aria-pressed', String(!shown));
            // keep focus on password input after toggle
            passwordEl.focus();
        }

        function onForgot(e) {
            e.preventDefault();
            window.open("https://app.remotepc.com/rpcnew/forgotPassword", "_blank");
        }

        function onCancel(e) {
            e && e.preventDefault();
            teardown();
            reject(new Error('User cancelled login'));
        }

        function onKeyDown(e) {
            if (e.key === 'Escape') {
                onCancel();
            }
        }

        async function onSubmit(e) {
            if (e) e.preventDefault();
            errorEl.style.display = 'none';
            const username = usernameEl.value.trim();
            const password = passwordEl.value;

            if (!username || !password) {
                errorEl.textContent = 'Please enter username and password';
                errorEl.style.display = 'block';
                return;
            }

            setLoading(true);

            try {
                // === DEV: simulate backend (replace with real call) ===
                await new Promise(r => setTimeout(r, 650));
                const token = _generateToken();
                const ttl = rememberEl.checked ? DEFAULT_TTL_MS * 24 : DEFAULT_TTL_MS;
                const expiresAt = Date.now() + ttl;
                _storeAuth({ username, token, expiresAt });
                teardown();
                resolve(username);
            } catch (err) {
                console.error('Login failed', err);
                errorEl.textContent = err.message || 'Sign-in failed';
                errorEl.style.display = 'block';
                setLoading(false);
            }
        }

        // attach listeners
        form.addEventListener('submit', onSubmit);
        cancelBtn.addEventListener('click', onCancel);
        toggleBtn.addEventListener('click', onToggle);
        forgotBtn.addEventListener('click', onForgot);
        document.addEventListener('keydown', onKeyDown);
    });
}


/* ---------- logout ---------- */
export async function logout() {
    console.log('AUTH: logout() called');
    try {
        localStorage.removeItem(STORAGE_KEY);
        _authCache = null;
        console.log('AUTH: localStorage cleared');
    } catch (e) {
        console.warn('AUTH: logout() cleanup error', e);
    }
}
