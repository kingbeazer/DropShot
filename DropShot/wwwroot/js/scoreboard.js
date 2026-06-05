let _handler = null;
let _voiceEnabled = false;
let _voiceRate = 0.9;

export function setVoiceEnabled(enabled) {
    _voiceEnabled = enabled;
    // Unlock the speech engine within the user-gesture that triggered this call.
    // Without this, browsers block speechSynthesis from non-gesture contexts (e.g. SignalR).
    if (enabled && window.speechSynthesis) {
        // iOS Safari requires a non-empty utterance to unlock the speech engine.
        // volume:0 keeps it silent while still satisfying the gesture requirement.
        const unlock = new SpeechSynthesisUtterance(' ');
        unlock.volume = 0;
        window.speechSynthesis.speak(unlock);
    }
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
