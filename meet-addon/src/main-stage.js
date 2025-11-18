// main stage source code
import { meet } from '@googleworkspace/meet-addons/meet.addons';
import { getUserEmail } from './identity.js';


export async function initializeMainStage() {
    const session = await meet.addon.createAddonSession({
        cloudProjectNumber: '792791698521'
    });
    const mainStageClient = await session.createMainStageClient();

    const startingState = await mainStageClient.getActivityStartingState();
    const initiator = startingState.additionalData;

    let myEmail;
    try {
        myEmail = await getUserEmail("silent");
    } catch {
        // If we truly can’t sign-in silently, just fall back to "host"
        myEmail = null;
    }

    console.log('initiator from startingState:', initiator);
    console.log('myEmail from Identity SDK:', myEmail);

    const greetingEl = document.getElementById('greeting');
    greetingEl.innerText = (initiator === myEmail)
        ? '👀 Hi viewer'
        : '🎩 Hi host';
   // await mainStageClient.notifySidePanel({ joined: myEmail });
}

/*npx webpack
firebase deploy*/

