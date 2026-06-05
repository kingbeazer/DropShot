let _handler = null;
let _voiceEnabled = false;
let _voiceRate = 0.9;
let _speechUnlocked = false;

function _unlockSpeech() {
    if (_speechUnlocked || !window.speechSynthesis) return;
    _speechUnlocked = true;
    // iOS (Safari + Chrome/WebKit) requires a non-empty utterance spoken
    // synchronously inside a user gesture to unlock speechSynthesis.
    const unlock = new SpeechSynthesisUtterance(' ');
    unlock.volume = 0;
    window.speechSynthesis.speak(unlock);
}

// Register native capture-phase listeners so the unlock fires synchronously on
// the first touch/click, before Blazor's async interop runs. Required on iOS
// where the gesture context expires before async callbacks reach JS.
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
    // Do NOT call cancel() here — on iOS it invalidates the speech permission
    // and the next speak() call is silently blocked.
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
