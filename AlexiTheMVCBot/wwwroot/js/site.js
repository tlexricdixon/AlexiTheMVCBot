// Get the target node (the element you want to observe)
const targetNode = document.getElementById('speakingClean');

// Create a new MutationObserver
const observer = new MutationObserver(async function (mutationsList) {
    for (let mutation of mutationsList) {
        if (mutation.type === 'childList') {
            const text = targetNode.textContent.trim(); // Use textContent instead of innerHTML
            if (text) {
                await speak(text);  // Call the speak function asynchronously
            }
        }
    }
});

// Options for the observer (which types of changes to observe)
const config = { childList: true };  // Observes when children are added or removed

// Start observing the target node for configured mutations
observer.observe(targetNode, config);

// Async speak function
async function speak(text) {
    const synth = window.speechSynthesis;

    // Ensure speech synthesis is supported
    if (!synth) {
        console.error("Speech synthesis not supported in this browser.");
        return;
    }

    const utterance = new SpeechSynthesisUtterance(text);
    const voices = await getVoices();
    console.log(voices);
    // Set voice to a woman's voice if available
    const femaleVoice = voices.find(voice => voice.name.includes('Zira'));
    if (femaleVoice) {
        utterance.voice = femaleVoice;
    } else if (voices.length > 0) {
        // Fallback: use the first available voice if no female voice is found
        utterance.voice = voices[0];
    }
    console.log(femaleVoice);
    utterance.pitch = 1; // Adjust pitch (0 to 2)
    utterance.rate = 1;  // Adjust rate (0.1 to 10)

    // Speak the utterance
    synth.speak(utterance);
}

// Function to get voices using a promise
function getVoices() {
    return new Promise((resolve) => {
        // Attempt to get voices immediately
        const voices = window.speechSynthesis.getVoices();
        if (voices.length > 0) {
            resolve(voices);
        } else {
            // Add an event listener for voiceschanged
            const onVoicesChanged = () => {
                const updatedVoices = window.speechSynthesis.getVoices();
                resolve(updatedVoices);
                window.speechSynthesis.removeEventListener("voiceschanged", onVoicesChanged);
            };
            window.speechSynthesis.addEventListener("voiceschanged", onVoicesChanged);
        }
    });
}

// Initial call to speak function
let text = targetNode.textContent.trim(); // Use textContent instead of innerHTML
if (text) {
    speak(text);
}
//// Get the target node (the element you want to observe)
//const targetNode = document.getElementById('speaking');

//// Create a new MutationObserver
//const observer = new MutationObserver(async function (mutationsList) {
//    for (let mutation of mutationsList) {
//        if (mutation.type === 'childList') {
//            // Wait for the text content to be fully populated
//            const text = targetNode.textContent.trim();
//            if (text) {
//                // Call the speak function asynchronously
//                await speak(text);
//            }
//        }
//    }
//});

//// Options for the observer (which types of changes to observe)
//const config = { childList: true }; // Observes when children are added or removed

//// Start observing the target node for configured mutations
//observer.observe(targetNode, config);

//// Async speak function
//async function speak(text) {
//    const synth = window.speechSynthesis;

//    // Ensure speech synthesis is supported
//    if (!synth) {
//        console.error("Speech synthesis not supported in this browser.");
//        return;
//    }

//    const utterance = new SpeechSynthesisUtterance(text);
//    const voices = await getVoices();
//    console.log(voices);

//    // Set voice to a woman's voice if available
//    const femaleVoice = voices.find(voice => voice.name.includes('Zira'));
//    if (femaleVoice) {
//        utterance.voice = femaleVoice;
//    } else if (voices.length > 0) {
//        // Fallback: use the first available voice if no female voice is found
//        utterance.voice = voices[0];
//    }
//    console.log(femaleVoice);
//    utterance.pitch = 1; // Adjust pitch (0 to 2)
//    utterance.rate = 1;  // Adjust rate (0.1 to 10)

//    // Speak the utterance
//    synth.speak(utterance);
//}

//// Function to get voices using a promise
//function getVoices() {
//    return new Promise((resolve) => {
//        // Attempt to get voices immediately
//        const voices = window.speechSynthesis.getVoices();
//        if (voices.length > 0) {
//            resolve(voices);
//        } else {
//            // Add an event listener for voiceschanged
//            const onVoicesChanged = () => {
//                const updatedVoices = window.speechSynthesis.getVoices();
//                resolve(updatedVoices);
//                window.speechSynthesis.removeEventListener("voiceschanged", onVoicesChanged);
//            };
//            window.speechSynthesis.addEventListener("voiceschanged", onVoicesChanged);
//        }
//    });
//}

//// Initial call to speak function if text is already populated
//let text = targetNode.textContent.trim();
//if (text) {
//    speak(text);
//}





