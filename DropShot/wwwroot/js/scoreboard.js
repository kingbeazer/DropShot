let _handler = null;

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
