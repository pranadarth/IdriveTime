//side-panel.js
import { meet } from '@googleworkspace/meet-addons/meet.addons';
import { getUserEmail } from './identity.js';
import { getAuthUser, logout } from './auth.js';

export async function setUpSidePanel() {
    // 1. Handshake
    const session = await meet.addon.createAddonSession({
        cloudProjectNumber: '792791698521'
    });
    const client = await session.createSidePanelClient();

    // 2. Starting state
    const startingState = await client.getActivityStartingState().catch(() => ({}));
    const isSessionStarted = Boolean(startingState.additionalData);

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
    // show signOut only in signed-in flows (you can also show per-branch)
    if (els.signOutBtn) els.signOutBtn.classList.remove('hidden');

    const sessionInitiator = startingState.additionalData;
    const storedInitiator = localStorage.getItem('RemotePC_Initiator');
    const initiator = sessionInitiator || storedInitiator;

    // debug
    console.log('sessionInitiator:', sessionInitiator);
    console.log('storedInitiator:', storedInitiator);
    console.log('initiator:', initiator);
    console.log('userEmail:', userEmail);
    console.log('isSessionStarted:', isSessionStarted);
    console.log('startingState:', startingState);

    // Show correct UI branch
    if (!initiator) {
        console.log('Flow: Host (first opener)');
        showHostUI();
    } else {
        const isHost = initiator === userEmail;
        if (isHost) {
            console.log('Flow: Host (returning)');
            showHostUI();
        } else if (isSessionStarted) {
            console.log('Flow: Participant');
            showParticipantUI();
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

    // -------------------------
    // UI helper functions (safe access)
    // -------------------------
    function showHostUI() {
        if (els.signOutBtn) els.signOutBtn.classList.remove('hidden');
        if (els.startActivityBtn) els.startActivityBtn.classList.remove('hidden');
        if (els.startActivityBtn) {
            els.startActivityBtn.onclick = () => {
                hideAll();
                if (els.createSessionBtn) els.createSessionBtn.classList.remove('hidden');
                if (els.hostMsg) els.hostMsg.classList.remove('hidden');

                client.startActivity({
                    sidePanelUrl: 'https://meet-addon-hosting-32017.web.app/SidePanel.html',
                    additionalData: userEmail
                }).catch(err => {
                    console.error('startActivity failed:', err);
                    if (err && err.message && err.message.includes('Operation cannot be performed while an activity is ongoing')) {
                        hideAll();
                        showWarningUI();
                    }
                });

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
                window.open('https://meet-addon-hosting-32017.web.app/DownloadRedirect.html', '_blank');
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

