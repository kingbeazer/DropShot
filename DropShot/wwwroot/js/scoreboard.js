let _handler = null;
let _voiceEnabled = false;
let _voiceRate = 0.9;
let _voiceURI = null;       // null = browser default

// ── iOS detection ────────────────────────────────────────────────────────────
const _isIOS = /iPad|iPhone|iPod/.test(navigator.userAgent) && !window.MSStream;

// ── Pre-recorded audio (iOS path) ────────────────────────────────────────────
let _audioUnlocked = false;  // true only after AudioContext is confirmed running
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

function _bufferDuration(key) {
    return _audioBuffers[key]?.duration ?? 0;
}

function _playBuffer(key, whenSeconds) {
    const buf = _audioBuffers[key];
    if (!buf || !_audioCtx) return;
    // If iOS suspended the context (e.g. after backgrounding), try to resume.
    if (_audioCtx.state === 'suspended') _audioCtx.resume();
    const src = _audioCtx.createBufferSource();
    src.buffer = buf;
    src.connect(_audioCtx.destination);
    src.start(whenSeconds ?? _audioCtx.currentTime);
}

// Called as many times as needed — only unlocks once the context is actually running.
// IMPORTANT: do NOT set _audioUnlocked = true until resume() confirms state === 'running'.
// If called outside a gesture, resume() silently fails on iOS; we must retry on the
// next gesture rather than assuming we're done.
function _unlockAudio() {
    if (!_isIOS) return;
    if (!_audioCtx) {
        _audioCtx = new (window.AudioContext || window.webkitAudioContext)();
    }
    if (_audioCtx.state === 'running') {
        if (!_audioUnlocked) {
            _audioUnlocked = true;
            _loadAudioBuffers();
        }
        return;
    }
    // Attempt resume — only succeeds when inside a user gesture on iOS.
    _audioCtx.resume().then(() => {
        if (_audioCtx.state === 'running' && !_audioUnlocked) {
            _audioUnlocked = true;
            _loadAudioBuffers();
        }
    });
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
// Re-registers itself until the AudioContext is confirmed running, so that a
// page-load call to setVoiceEnabled() (outside a gesture) doesn't permanently
// block the proper gesture-based unlock.
export function initVoiceUnlock() {
    const handler = () => {
        _unlockSpeech();
        _unlockAudio();
        // Re-register if the AudioContext still isn't running after this gesture.
        if (_isIOS && _audioCtx && _audioCtx.state !== 'running') {
            document.addEventListener('touchstart', handler, { capture: true, once: true });
            document.addEventListener('click',      handler, { capture: true, once: true });
        }
    };
    document.addEventListener('touchstart', handler, { capture: true, once: true });
    document.addEventListener('click',      handler, { capture: true, once: true });
}

export function setVoiceEnabled(enabled) {
    _voiceEnabled = enabled;
    if (enabled) {
        _unlockSpeech();
        _unlockAudio();  // May not fully unlock outside a gesture — initVoiceUnlock handles the rest.
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
export function announceScore(text, primaryAudioKey, followUpText, followUpAudioKey, followUpDelayMs) {
    if (!_voiceEnabled) return;

    const delay = followUpDelayMs ?? 1400;

    if (_isIOS) {
        if (primaryAudioKey && _audioBuffers[primaryAudioKey]) {
            _playBuffer(primaryAudioKey);
            if (followUpAudioKey && _audioBuffers[followUpAudioKey]) {
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

// ── Tiebreak ─────────────────────────────────────────────────────────────────
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
