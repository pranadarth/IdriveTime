// side-panel.js
import { meet } from '@googleworkspace/meet-addons/meet.addons';
import { getUserEmail } from './identity.js';
import { getAuthUser, logout } from './auth.js';

export async function setUpSidePanel() {
    // 1. Handshake
    const session = await meet.addon.createAddonSession({
        cloudProjectNumber: '792791698521'
    });
    const client = await session.createSidePanelClient();

    // 2. Starting state (must fetch before trying to read additionalData)
    const startingState = await client.getActivityStartingState().catch(() => ({}));
    const isSessionStarted = Boolean(startingState.additionalData);

    // 2a. Parse additionalData
    let startingPayload = null;
    if (startingState && typeof startingState.additionalData === 'string' && startingState.additionalData.trim()) {
        try {
            startingPayload = JSON.parse(startingState.additionalData);
        } catch (e) {
            // fallback: previously we might have stored plain string (initiator email)
            startingPayload = { initiator: startingState.additionalData };
        }
    }
    // Now get clientId/initiator conveniently:
    const sessionInitiatorFromPayload = startingPayload && startingPayload.initiator;
    const sessionClientIdFromPayload = startingPayload && startingPayload.clientId;

    const ATTENDED_API_URL = 'https://web1.remotepc.com/rpcnew/api/ota/v3/generate';

    // 3. Elements (may be null in some test states)
    const els = {
        signInBtn: document.getElementById('signInBtn'),
        signOutBtn: document.getElementById('signOutBtn'),
        startActivityBtn: document.getElementById('startActivityBtn'),
        createSessionBtn: document.getElementById('createSessionBtn'),
        hostMsg: document.getElementById('hostMsg'),
        downloadBtn: document.getElementById('downloadBtn'),
        participantMsg: document.getElementById('participantMsg'),
        warnMsg: document.getElementById('warnMsg')
    };

    // 4. Safe hide helper
    function hideAll() {
        Object.values(els).forEach(el => { if (el) el.classList.add('hidden'); });
    }

    // create attended/session id via API
    async function createAttendedDirect(preferredId) {
        const idToUse = (preferredId || `web-${Math.random().toString(36).slice(2, 9)}`) + '_RPCAT';
        const version = '1.0.0';

        const form = new URLSearchParams();
        form.append('os', '1');
        form.append('id', idToUse);
        form.append('version', version);

        console.log('ATTENDED: request ->', { url: ATTENDED_API_URL, id: idToUse });

        const resp = await fetch(ATTENDED_API_URL, {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: form.toString()
        });

        if (!resp.ok) {
            const body = await resp.text().catch(() => '<no-body>');
            console.error('ATTENDED: non-OK response', resp.status, body);
            throw new Error(`Attended API returned ${resp.status}`);
        }

        const json = await resp.json().catch(err => {
            console.error('ATTENDED: JSON parse error', err);
            throw new Error('Invalid JSON from attended API');
        });

        console.log('ATTENDED: API response:', json);
        return { raw: json, usedId: idToUse };
    }

    // show session card UI (centered single-row layout)
    function showSessionCard(clientId) {
        let card = document.getElementById('rpc-session-card');
        if (!card) {
            card = document.createElement('div');
            card.id = 'rpc-session-card';
            card.style.marginTop = '12px';
            card.style.display = 'flex';
            card.style.flexDirection = 'column';
            card.style.alignItems = 'center';
            card.style.gap = '6px';
            card.innerHTML = `
      <div id="rpc-session-row" style="
        display:flex;
        align-items:center;
        gap:10px;
        width:100%;
        justify-content:center;
      ">
        <div style="font-weight:600; color:#202124; white-space:nowrap;">Session ID:</div>
        <div id="rpc-session-value" style="
          font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, 'Roboto Mono', monospace;
          font-size:14px;
          padding:8px 12px;
          border-radius:8px;
          background:#f1f3f4;
          color:#202124;
          user-select:text;
          max-width:220px;
          overflow:hidden;
          text-overflow:ellipsis;
          white-space:nowrap;
          text-align:center;
        " title="${clientId}">${clientId}</div>

        <button id="rpc-copy-session" aria-label="Copy session id" title="Copy session ID" style="
          display:inline-flex;
          align-items:center;
          justify-content:center;
          padding:8px;
          border-radius:8px;
          border:1px solid rgba(0,0,0,0.08);
          background:#fff;
          cursor:pointer;
          min-width:44px;
          height:40px;
          flex:0 0 44px;
        ">
          <span id="rpc-copy-icon" style="display:inline-block;">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true">
              <path d="M16 1H4c-1.1 0-2 .9-2 2v12h2V3h12V1z" fill="#5f6368"/>
              <path d="M20 5H8c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V7c0-1.1-.9-2-2-2zm0 16H8V7h12v14z" fill="#5f6368"/>
            </svg>
          </span>
          <span id="rpc-copy-spinner" style="display:none;">
            <svg width="16" height="16" viewBox="0 0 24 24" aria-hidden="true">
              <circle cx="12" cy="12" r="10" stroke="#1a73e8" stroke-width="2" stroke-opacity="0.25" fill="none"></circle>
              <path d="M22 12a10 10 0 00-10-10" stroke="#1a73e8" stroke-width="2" stroke-linecap="round"></path>
            </svg>
          </span>
          <span id="rpc-copy-check" style="display:none;">
            <svg width="16" height="16" viewBox="0 0 24 24" aria-hidden="true">
              <path d="M20 6L9 17l-5-5" stroke="#1a73e8" stroke-width="2" fill="none" stroke-linecap="round" stroke-linejoin="round"></path>
            </svg>
          </span>
        </button>
      </div>
      <div id="rpc-session-hint" style="font-size:12px;color:#5f6368; display:none; text-align:center;"></div>
    `;

            const preferredAnchor = document.getElementById('downloadBtn') || document.getElementById('createSessionBtn');
            if (preferredAnchor && preferredAnchor.parentNode) {
                // insert card right after downloadBtn (preferred) or after createSessionBtn
                preferredAnchor.parentNode.insertBefore(card, preferredAnchor.nextSibling);
            } else {
                // fallback: append to container
                const container = document.querySelector('.container');
                if (container) container.appendChild(card);
                else document.body.appendChild(card);
            }
        } else {
            // update value and title
            const val = document.getElementById('rpc-session-value');
            if (val) {
                val.innerText = clientId;
                val.title = clientId;
            }
            card.classList.remove('hidden');
        }

        // refs
        const copyBtn = document.getElementById('rpc-copy-session');
        const valueEl = document.getElementById('rpc-session-value');
        const hintEl = document.getElementById('rpc-session-hint');
        const iconSpan = document.getElementById('rpc-copy-icon');
        const spinnerSpan = document.getElementById('rpc-copy-spinner');
        const checkSpan = document.getElementById('rpc-copy-check');

        if (!copyBtn || !valueEl) return;

        // remove prior listeners 
        const freshBtn = copyBtn.cloneNode(true);
        copyBtn.parentNode.replaceChild(freshBtn, copyBtn);

        freshBtn.addEventListener('click', async (ev) => {
            ev.preventDefault();
            const textToCopy = (valueEl.innerText || '').trim();
            console.log('COPY: attempt ->', textToCopy);
            if (!textToCopy) {
                hintEl.innerText = 'Nothing to copy';
                hintEl.style.display = 'block';
                setTimeout(() => { hintEl.style.display = 'none'; }, 1200);
                return;
            }

            // show spinner
            if (iconSpan) iconSpan.style.display = 'none';
            if (checkSpan) checkSpan.style.display = 'none';
            if (spinnerSpan) spinnerSpan.style.display = '';

            freshBtn.disabled = true;
            let success = false;

            try {
                if (navigator.clipboard && navigator.clipboard.writeText) {
                    await navigator.clipboard.writeText(textToCopy);
                    console.info('COPY: navigator.clipboard succeeded');
                    success = true;
                } else {
                    throw new Error('navigator.clipboard unavailable');
                }
            } catch (err) {
                console.warn('COPY: clipboard failed, trying fallback', err);
                try {
                    const ta = document.createElement('textarea');
                    ta.value = textToCopy;
                    ta.style.position = 'fixed';
                    ta.style.left = '-9999px';
                    document.body.appendChild(ta);
                    ta.select();
                    const ok = document.execCommand('copy');
                    document.body.removeChild(ta);
                    success = !!ok;
                    console.info('COPY: execCommand fallback result =', ok);
                } catch (err2) {
                    console.error('COPY: fallback failed', err2);
                    success = false;
                }
            }

            // update UI
            if (spinnerSpan) spinnerSpan.style.display = 'none';
            if (success) {
                if (checkSpan) checkSpan.style.display = '';
                hintEl.innerText = 'Copied!';
                hintEl.style.display = 'block';
                console.log('COPY: success ->', textToCopy);
            } else {
                if (iconSpan) iconSpan.style.display = '';
                hintEl.innerText = 'Copy failed';
                hintEl.style.display = 'block';
                console.warn('COPY: failed both methods');
            }

            // restore button state
            setTimeout(() => {
                if (checkSpan) checkSpan.style.display = 'none';
                if (iconSpan) iconSpan.style.display = '';
                freshBtn.disabled = false;
                hintEl.style.display = 'none';
            }, 1300);
        });
    }

    // Utility to show a small inline error in your warnMsg area
    function showInlineError(msg) {
        const warn = document.getElementById('warnMsg') || (els && els.warnMsg);
        if (warn) {
            warn.innerText = msg;
            warn.classList.remove('hidden');
        } else {
            console.error('Warning area not found:', msg);
        }
    }

    // safe wiring of createSessionBtn
    if (els.createSessionBtn) {
        els.createSessionBtn.onclick = () => {
            window.open('https://login.remotepc.com/rpcnew/home', '_blank');
        };
    }

    // 5. Auth check
    let userEmail = null;
    try {
        userEmail = await getAuthUser('silent');
    } catch (e) {
        // not signed in or user dismissed
    }

    // 6. Not signed in → show sign-in only
    if (!userEmail) {
        console.log('Flow: Not signed in');
        hideAll();
        if (els.signOutBtn) els.signOutBtn.classList.add('hidden');
        if (els.signInBtn) {
            els.signInBtn.classList.remove('hidden');
            els.signInBtn.onclick = async () => {
                await getAuthUser('prompt');
                window.location.reload();
            };
        }
        return;
    }

    // 7. Signed-in flows
    hideAll();
    if (els.signOutBtn) els.signOutBtn.classList.remove('hidden');

    // Choose the authoritative initiator:
    const sessionInitiator = sessionInitiatorFromPayload || localStorage.getItem('RemotePC_Initiator');
    const sessionClientId = sessionClientIdFromPayload || localStorage.getItem('RemotePC_SessionClientId');

    // debug
    console.log('sessionInitiator (payload/local):', sessionInitiator);
    console.log('sessionClientId (payload/local):', sessionClientId);
    console.log('userEmail:', userEmail);
    console.log('isSessionStarted:', isSessionStarted);
    console.log('startingState:', startingState);

    // Show correct UI branch
    if (!sessionInitiator) {
        console.log('Flow: Host (first opener)');
        showHostUI();
    } else {
        const isHost = sessionInitiator === userEmail;
        if (isHost) {
            console.log('Flow: Host (returning)');
            showHostUI();
        } else if (isSessionStarted) {
            console.log('Flow: Participant');
            showParticipantUI();
            // if additionalData provided a clientId, show session card for participants
            if (sessionClientId) showSessionCard(sessionClientId);
        } else {
            console.log('Flow: Premature non-host (warning)');
            showWarningUI();
        }
    }

    // -------------------------
    // Expose current state globally so delegated handler sees up-to-date values
    // -------------------------
    window.__rpc_signout_currentUser = userEmail;
    window.__rpc_signout_startingState = startingState;

    // -------------------------
    // Delegated sign-out handler (attach once)
    // -------------------------
    if (!window.__rpc_signout_delegated_attached) {
        window.__rpc_signout_delegated_attached = true;

        document.addEventListener('click', async function rpc_signout_delegated(e) {
            const btn = e.target.closest && e.target.closest('#signOutBtn');
            if (!btn) return;

            e.preventDefault();
            const currentUser = window.__rpc_signout_currentUser;
            const currentStartingState = window.__rpc_signout_startingState || {};

            console.log('SIGNOUT: delegated start — userEmail:', currentUser);

            try {
                const sessionInitiator = currentStartingState && currentStartingState.additionalData;
                const storedInitiator = localStorage.getItem('RemotePC_Initiator');
                const amInitiator = (storedInitiator && storedInitiator === currentUser) ||
                    (sessionInitiator && sessionInitiator === currentUser);

                if (amInitiator) {
                    const ok = await showConfirm('You are the session initiator. End the session and sign out?');
                    if (!ok) { console.log('SIGNOUT: cancelled (initiator)'); return; }
                    localStorage.removeItem('RemotePC_Initiator');
                    console.log('SIGNOUT: removed RemotePC_Initiator');
                } else {
                    const ok = await showConfirm('Sign out now?');
                    if (!ok) { console.log('SIGNOUT: cancelled (participant)'); return; }
                }

                // call imported logout() from auth.js
                if (typeof logout === 'function') {
                    try {
                        await logout();
                        console.log('SIGNOUT: logout() OK');
                    } catch (err) {
                        console.warn('SIGNOUT: logout() threw', err);
                    }
                } else {
                    localStorage.removeItem('RemotePC_Auth_v1');
                    console.log('SIGNOUT: fallback auth key removed');
                }

                // cleanup GSI fallback
                try { sessionStorage.removeItem('meetAddonEmail'); } catch (e) { /*ignore*/ }
                if (window.google && google.accounts && google.accounts.id && google.accounts.id.disableAutoSelect) {
                    try { google.accounts.id.disableAutoSelect(); console.log('SIGNOUT: disabled GSI autoSelect'); } catch (e) { /*ignore*/ }
                }

            } catch (err) {
                console.error('SIGNOUT: unexpected', err);
            } finally {
                console.log('SIGNOUT: reloading UI');
                window.location.reload();
            }
        }, { capture: false });
    }

    // Host flow helper (creates attended session first, then starts activity)
    async function onHostStartActivity(client, userEmail, els) {
        try {
            // immediate UI flip
            hideAll();
            if (els.createSessionBtn) {
                els.createSessionBtn.classList.remove('hidden');
                els.createSessionBtn.disabled = true;
                els.createSessionBtn.innerText = 'Creating…';
            }
            if (els.hostMsg) els.hostMsg.classList.remove('hidden');

            // 1) create attended/session first
            console.log('HOST: creating attended session for', userEmail);
            const attended = await createAttendedDirect(userEmail);
            const json = attended.raw;

            if (!json || json.status !== 'OK' || !json.message) {
                console.error('ATTENDED: invalid response', json);
                throw new Error('Invalid response from attended API');
            }

            // pick client id (or fallback to usedId)
            const clientId = json.message.client_id || json.message.access_token || attended.usedId;
            console.log('HOST: got clientId =', clientId);

            // 2) now start the activity and include session id in additionalData
            const additionalData = JSON.stringify({
                initiator: userEmail,
                clientId
            });

            try {
                await client.startActivity({
                    sidePanelUrl: 'https://meet-addon-hosting-32017.web.app/SidePanel.html',
                    additionalData
                });
            } catch (err) {
                if (!/single instance|already active/i.test(err.message)) {
                    console.error('HOST: startActivity failed', err);
                } else {
                    console.log('HOST: activity already active — ignoring duplicate start');
                }
            }

            // 3) persist locally for future checks & update UI
            localStorage.setItem('RemotePC_Initiator', userEmail);
            localStorage.setItem('RemotePC_SessionClientId', clientId);

            if (els.createSessionBtn) {
                els.createSessionBtn.disabled = false;
                els.createSessionBtn.innerText = 'Create a Session';
            }
            showSessionCard(clientId);
        } catch (err) {
            console.error('Create session error:', err);
            const msg = (err.message && err.message.includes('Failed to fetch'))
                ? 'Network/CORS error when calling attended API. See console.'
                : `Failed to create session: ${err.message || 'unknown'}`;
            showInlineError(msg);

            // restore host UI so they can retry
            hideAll();
            if (els.startActivityBtn) els.startActivityBtn.classList.remove('hidden');
            if (els.createSessionBtn) {
                els.createSessionBtn.classList.remove('hidden');
                els.createSessionBtn.disabled = false;
                els.createSessionBtn.innerText = 'Create a Session';
            }
            if (els.hostMsg) els.hostMsg.classList.remove('hidden');
        }
    }

    // -------------------------
    // UI helper functions (safe access)
    // -------------------------
    function showHostUI() {
        if (els.signOutBtn) els.signOutBtn.classList.remove('hidden');
        if (els.startActivityBtn) els.startActivityBtn.classList.remove('hidden');
        if (els.startActivityBtn) {
            els.startActivityBtn.onclick = () => {
                onHostStartActivity(client, userEmail, els);
                localStorage.setItem('RemotePC_Initiator', userEmail);
            };
        }
    }

    function showParticipantUI() {
        if (els.signOutBtn) els.signOutBtn.classList.remove('hidden');
        if (els.downloadBtn) els.downloadBtn.classList.remove('hidden');
        if (els.participantMsg) els.participantMsg.classList.remove('hidden');
        if (els.downloadBtn) {
            els.downloadBtn.onclick = () => {
                const clientId = sessionClientIdFromPayload || localStorage.getItem('RemotePC_SessionClientId') || '';
                const params = new URLSearchParams();
                if (clientId) params.set('clientId', clientId);
                const url = 'https://meet-addon-hosting-32017.web.app/DownloadRedirect.html' + (params.toString() ? ('?' + params.toString()) : '');
                console.log('DOWNLOAD: opening', url);
                window.open(url, '_blank');
            };
        }
    }

    function showWarningUI() {
        if (els.warnMsg) els.warnMsg.classList.remove('hidden');
    }

    function showConfirm(message = 'Are you sure?') {
        return new Promise(resolve => {
            const overlay = document.getElementById('rpcConfirmOverlay');
            const msgEl = document.getElementById('rpcConfirmMsg');
            const yesBtn = document.getElementById('rpcConfirmYes');
            const noBtn = document.getElementById('rpcConfirmNo');
            if (!overlay || !yesBtn || !noBtn || !msgEl) return resolve(false);

            msgEl.innerText = message;
            overlay.classList.remove('hidden');
            overlay.setAttribute('aria-hidden', 'false');

            // handlers (one-time)
            function cleanAndResolve(result) {
                overlay.classList.add('hidden');
                overlay.setAttribute('aria-hidden', 'true');
                yesBtn.removeEventListener('click', onYes);
                noBtn.removeEventListener('click', onNo);
                resolve(result);
            }
            function onYes(e) { e.preventDefault(); cleanAndResolve(true); }
            function onNo(e) { e.preventDefault(); cleanAndResolve(false); }

            yesBtn.addEventListener('click', onYes);
            noBtn.addEventListener('click', onNo);
        });
    }
}
