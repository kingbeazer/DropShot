let _handler = null;
let _voiceEnabled = false;
let _voiceRate = 0.9;
let _voiceURI = null;       // null = browser default

// ── iOS detection ────────────────────────────────────────────────────────────
const _isIOS = /iPad|iPhone|iPod/.test(navigator.userAgent) && !window.MSStream;

// ── Pre-recorded audio (iOS path) ────────────────────────────────────────────
let _audioUnlocked = false;
let _audioBuffers = {};
let _audioCtx = null;

async function _loadAudioBuffers() {
    const keys = [
        'love-all','fifteen-love','love-fifteen','fifteen-all',
        'thirty-love','love-thirty','thirty-fifteen','fifteen-thirty','thirty-all',
        'forty-love','love-forty','forty-fifteen','fifteen-forty',
        'forty-thirty','thirty-forty',
        'deuce','advantage-server','advantage-receiver',
        'game-server','game-receiver',
        'set-server','set-receiver',
        'match-server','match-receiver',
        ...Array.from({length:14}, (_,i) => `tb-${i}`)
    ];
    await Promise.all(keys.map(async key => {
        try {
            const res = await fetch(`/audio/${key}.wav`);
            const buf = await res.arrayBuffer();
            _audioBuffers[key] = await _audioCtx.decodeAudioData(buf);
        } catch { /* ignore missing files */ }
    }));
}

// Returns the duration (seconds) of the buffer, or 0 if not loaded.
function _bufferDuration(key) {
    return _audioBuffers[key]?.duration ?? 0;
}

function _playBuffer(key, whenSeconds) {
    const buf = _audioBuffers[key];
    if (!buf || !_audioCtx) return;
    const src = _audioCtx.createBufferSource();
    src.buffer = buf;
    src.connect(_audioCtx.destination);
    src.start(whenSeconds ?? _audioCtx.currentTime);
}

function _unlockAudio() {
    if (_audioUnlocked || !_isIOS) return;
    _audioUnlocked = true;
    if (!_audioCtx) _audioCtx = new (window.AudioContext || window.webkitAudioContext)();
    _audioCtx.resume().then(() => _loadAudioBuffers());
}

// ── Web Speech API (desktop path) ────────────────────────────────────────────
let _speechUnlocked = false;
let _keepalive = null;

function _unlockSpeech() {
    if (_speechUnlocked || !window.speechSynthesis) return;
    _speechUnlocked = true;
    const unlock = new SpeechSynthesisUtterance(' ');
    unlock.volume = 0;
    window.speechSynthesis.speak(unlock);
    window.speechSynthesis.resume();
}

function _makeUtt(text) {
    const utt = new SpeechSynthesisUtterance(text);
    utt.rate = _voiceRate;
    if (_voiceURI) {
        const voices = window.speechSynthesis.getVoices();
        const v = voices.find(v => v.voiceURI === _voiceURI);
        if (v) utt.voice = v;
    }
    return utt;
}

// ── Shared unlock on first gesture ───────────────────────────────────────────
export function initVoiceUnlock() {
    const handler = () => {
        _unlockSpeech();
        _unlockAudio();
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
        _unlockAudio();
        if (!_isIOS && !_keepalive) {
            _keepalive = setInterval(() => {
                if (!window.speechSynthesis || window.speechSynthesis.speaking) return;
                window.speechSynthesis.pause();
                window.speechSynthesis.resume();
            }, 10000);
        }
    } else {
        if (_keepalive) { clearInterval(_keepalive); _keepalive = null; }
    }
}

export function setVoiceURI(uri) {
    _voiceURI = uri || null;
}

// Returns [{name, voiceURI, lang}] for English voices available on this device.
export function getAvailableVoices() {
    if (!window.speechSynthesis) return [];
    return window.speechSynthesis.getVoices()
        .filter(v => v.lang.startsWith('en'))
        .map(v => ({ name: v.name, voiceURI: v.voiceURI, lang: v.lang }));
}

// ── Core announcement ─────────────────────────────────────────────────────────
// primaryAudioKey  – pre-recorded file for iOS
// followUpText     – optional text to speak after a delay (desktop)
// followUpAudioKey – pre-recorded file for the follow-up (iOS)
// followUpDelayMs  – gap before follow-up (default 1400 ms)
export function announceScore(text, primaryAudioKey, followUpText, followUpAudioKey, followUpDelayMs) {
    if (!_voiceEnabled) return;

    const delay = followUpDelayMs ?? 1400;

    if (_isIOS) {
        if (primaryAudioKey && _audioBuffers[primaryAudioKey]) {
            _playBuffer(primaryAudioKey);
            if (followUpAudioKey && _audioBuffers[followUpAudioKey]) {
                // Schedule follow-up after the primary clip finishes + gap
                const gap = _bufferDuration(primaryAudioKey) * 1000 + delay;
                setTimeout(() => _playBuffer(followUpAudioKey), gap);
            }
        }
    } else {
        if (!window.speechSynthesis) return;
        window.speechSynthesis.speak(_makeUtt(text));
        window.speechSynthesis.resume();
        if (followUpText) {
            setTimeout(() => {
                if (!_voiceEnabled) return;
                window.speechSynthesis.speak(_makeUtt(followUpText));
                window.speechSynthesis.resume();
            }, delay);
        }
    }
}

// ── Tiebreak (iOS): two numbered clips with a gap ────────────────────────────
export function announceTiebreak(userPts, oppPts) {
    if (!_voiceEnabled) return;
    if (_isIOS) {
        _playBuffer(`tb-${userPts}`);
        setTimeout(() => _playBuffer(`tb-${oppPts}`), 600);
    } else {
        if (!window.speechSynthesis) return;
        window.speechSynthesis.speak(_makeUtt(`${userPts}, ${oppPts}`));
        window.speechSynthesis.resume();
    }
}

// ── ESC / fullscreen ─────────────────────────────────────────────────────────
export function registerEscHandler(dotNetRef) {
    unregisterEscHandler();
    _handler = function (e) {
        if (e.key === 'Escape') dotNetRef.invokeMethodAsync('ExitFullscreenFromJs');
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
    document.body.classList.toggle('scoreboard-fullscreen', enabled);
}

export function setScoreboardActive(enabled) {
    document.body.classList.toggle('scoreboard-active', enabled);
}
