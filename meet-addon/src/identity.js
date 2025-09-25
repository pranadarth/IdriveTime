// src/identity.js
/**
 * Loads GSI, prompts for one-tap sign in if needed, and resolves to the user's email.
 * Caches the result so subsequent calls return immediately.
 */

const CLIENT_ID = '792791698521-d99qe7hhcstqa58iefpuqdel87lradsd.apps.googleusercontent.com';
const STORAGE_KEY = 'meetAddonEmail';

let _emailCache = null;
let _gsiInitialized = false;

/**
 * Ensures google.accounts.id is initialized only once.
 * @param {boolean} autoSelect – if true, GSI will try silent sign-in
 */
function _ensureGsi(autoSelect) {
    if (_gsiInitialized) return;
    google.accounts.id.initialize({
        client_id: CLIENT_ID,
        auto_select: !!autoSelect,
        cancel_on_tap_outside: false,
        callback: (resp) => {
            try {
                const payload = JSON.parse(atob(resp.credential.split('.')[1]));
                _emailCache = payload.email;
                sessionStorage.setItem(STORAGE_KEY, _emailCache);
            } catch (e) {
                console.error('Failed to parse GSI credential', e);
            }
        }
    });
    _gsiInitialized = true;
}

/**
 * Fetches the signed-in user’s email.
 * @param {'silent'|'prompt'} mode
 *   - 'silent': only auto_select, no UI. rejects if no session.
 *   - 'prompt': tries silent first, then shows the one-tap prompt.
 */
export function getUserEmail(mode = 'silent') {
    // 1) In-memory cache
    if (_emailCache) {
        return Promise.resolve(_emailCache);
    }
    // 2) sessionStorage cache
    const stored = sessionStorage.getItem(STORAGE_KEY);
    if (stored) {
        _emailCache = stored;
        return Promise.resolve(stored);
    }
    // 3) Fresh GSI flow
    return new Promise((resolve, reject) => {
        _ensureGsi(mode === 'silent');

        if (mode === 'silent') {
            // Try silent only
            google.accounts.id.prompt((notification) => {
                if (_emailCache) {
                    resolve(_emailCache);
                } else {
                    reject(new Error('Silent sign-in failed'));
                }
            });
            return;
        }

        // Prompt mode: first silent, then visible
        let triedSilent = false;
        const promptCb = (notification) => {
            if (!triedSilent) {
                triedSilent = true;
                if (!_emailCache) {
                    google.accounts.id.prompt();
                }
            } else {
                if (_emailCache) {
                    resolve(_emailCache);
                } else {
                    reject(new Error('Prompt sign-in failed'));
                }
            }
        };
        google.accounts.id.prompt(promptCb);
    });
}
