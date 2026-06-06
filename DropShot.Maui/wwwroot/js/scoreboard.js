let _handler = null;
let _voiceEnabled = false;
let _voiceRate = 0.9;

// ── iOS detection ────────────────────────────────────────────────────────────
// iOS WebKit blocks speechSynthesis.speak() from non-gesture contexts (e.g.
// SignalR).  We use pre-recorded <audio> elements instead, which CAN be played
// from non-gesture contexts once unlocked by a user gesture.
const _isIOS = /iPad|iPhone|iPod/.test(navigator.userAgent) && !window.MSStream;

// ── Pre-recorded audio (iOS path) ────────────────────────────────────────────
let _audioUnlocked = false;
let _audioBuffers = {};   // key → AudioBuffer
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
    const fetches = keys.map(async key => {
        try {
            const res = await fetch(`/audio/${key}.wav`);
            const buf = await res.arrayBuffer();
            _audioBuffers[key] = await _audioCtx.decodeAudioData(buf);
        } catch { /* ignore missing files */ }
    });
    await Promise.all(fetches);
}

function _playBuffer(key) {
    const buf = _audioBuffers[key];
    if (!buf || !_audioCtx) return;
    const src = _audioCtx.createBufferSource();
    src.buffer = buf;
    src.connect(_audioCtx.destination);
    src.start();
}

function _unlockAudio() {
    if (_audioUnlocked || !_isIOS) return;
    _audioUnlocked = true;
    if (!_audioCtx) {
        _audioCtx = new (window.AudioContext || window.webkitAudioContext)();
    }
    _audioCtx.resume().then(() => _loadAudioBuffers());
}

// ── Web Speech API (desktop path) ────────────────────────────────────────────
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
        if (!_isIOS && !_iosKeepalive) {
            _iosKeepalive = setInterval(() => {
                if (!window.speechSynthesis || window.speechSynthesis.speaking) return;
                window.speechSynthesis.pause();
                window.speechSynthesis.resume();
            }, 10000);
        }
    } else {
        if (_iosKeepalive) { clearInterval(_iosKeepalive); _iosKeepalive = null; }
    }
}

// audioKey maps to a pre-recorded file name (may be null for tiebreak etc.)
export function announceScore(text, audioKey) {
    if (!_voiceEnabled) return;

    if (_isIOS) {
        // iOS: use pre-recorded audio only
        if (audioKey && _audioBuffers[audioKey]) {
            _playBuffer(audioKey);
        }
    } else {
        // Desktop: use Web Speech API
        if (!window.speechSynthesis) return;
        const utt = new SpeechSynthesisUtterance(text);
        utt.rate = _voiceRate;
        window.speechSynthesis.speak(utt);
        window.speechSynthesis.resume();
    }
}

// ── Tiebreak (iOS): play two number buffers with a short gap ─────────────────
export function announceTiebreak(userPts, oppPts) {
    if (!_voiceEnabled || !_isIOS) return;
    _playBuffer(`tb-${userPts}`);
    setTimeout(() => _playBuffer(`tb-${oppPts}`), 600);
}

// ── ESC / fullscreen ─────────────────────────────────────────────────────────
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
