window.generateQrCode = function (elementId, url) {
    var qr = qrcode(0, 'M');
    qr.addData(url);
    qr.make();
    var el = document.getElementById(elementId);
    if (el) el.innerHTML = qr.createSvgTag({ cellSize: 6, margin: 4 });
};

window.copyToClipboard = function (text) {
    return navigator.clipboard.writeText(text);
};
