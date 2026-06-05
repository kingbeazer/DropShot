let _handler = null;
let _voiceEnabled = false;
let _voiceRate = 0.9;
let _speechUnlocked = false;

function _unlockSpeech() {
    if (_speechUnlocked || !window.speechSynthesis) return;
    _speechUnlocked = true;
    // Speak a silent non-empty utterance synchronously within the user gesture.
    // Required on iOS (Safari and Chrome/WebKit) — an empty string is ignored,
    // and the unlock must happen synchronously inside the gesture handler.
    const unlock = new SpeechSynthesisUtterance(' ');
    unlock.volume = 0;
    window.speechSynthesis.speak(unlock);
}

// Attach native capture-phase listeners so the unlock fires synchronously on
// the very first touch/click, before Blazor's async interop can run. This is
// necessary on iOS where the gesture context expires before async callbacks fire.
export function initVoiceUnlock() {
    const handler = () => {
        _unlockSpeech();
        document.removeEventListener('touchstart', handler, true);
        document.removeEventListener('click', handler, true);
    };
    document.addEventListener('touchstart', handler, { capture: true, once: true });
    document.addEventListener('click', handler, { capture: true, once: true });
}

export function setVoiceEnabled(enabled) {
    _voiceEnabled = enabled;
    if (enabled) _unlockSpeech();
}

export function announceScore(text) {
    if (!_voiceEnabled) return;
    if (!window.speechSynthesis) return;
    window.speechSynthesis.cancel();
    const utt = new SpeechSynthesisUtterance(text);
    utt.rate = _voiceRate;
    window.speechSynthesis.speak(utt);
}

export function registerEscHandler(dotNetRef) {
    unregisterEscHandler();
    _handler = function (e) {
        if (e.key === 'Escape') {
            dotNetRef.invokeMethodAsync('ExitFullscreenFromJs');
        }
    };
    document.addEventListener('keydown', _handler);
}

export function unregisterEscHandler() {
    if (_handler) {
        document.removeEventListener('keydown', _handler);
        _handler = null;
    }
}

export function setFullscreen(enabled) {
    if (enabled) {
        document.body.classList.add('scoreboard-fullscreen');
    } else {
        document.body.classList.remove('scoreboard-fullscreen');
    }
}

export function setScoreboardActive(enabled) {
    if (enabled) {
        document.body.classList.add('scoreboard-active');
    } else {
        document.body.classList.remove('scoreboard-active');
    }
}
