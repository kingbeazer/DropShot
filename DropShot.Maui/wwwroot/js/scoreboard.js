let _handler = null;
let _voiceEnabled = false;
let _voiceRate = 0.9;
let _speechUnlocked = false;
let _iosKeepalive = null;

function _unlockSpeech() {
    if (_speechUnlocked || !window.speechSynthesis) return;
    _speechUnlocked = true;
    const unlock = new SpeechSynthesisUtterance(' ');
    unlock.volume = 0;
    window.speechSynthesis.speak(unlock);
    window.speechSynthesis.resume();
}

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
    if (enabled) {
        _unlockSpeech();
        if (!_iosKeepalive) {
            _iosKeepalive = setInterval(() => {
                if (!window.speechSynthesis || window.speechSynthesis.speaking) return;
                window.speechSynthesis.pause();
                window.speechSynthesis.resume();
            }, 10000);
        }
    } else {
        if (_iosKeepalive) {
            clearInterval(_iosKeepalive);
            _iosKeepalive = null;
        }
    }
}

export function announceScore(text) {
    if (!_voiceEnabled) return;
    if (!window.speechSynthesis) return;
    const utt = new SpeechSynthesisUtterance(text);
    utt.rate = _voiceRate;
    window.speechSynthesis.speak(utt);
    window.speechSynthesis.resume();
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
