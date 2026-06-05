(function () {
  function drawQr(canvas, text) {
    if (typeof qrcode !== "function") {
      throw new Error("QR generator library is not loaded.");
    }

    const qr = qrcode(0, "M");
    qr.addData(text);
    qr.make();

    const ctx = canvas.getContext("2d");
    const moduleCount = qr.getModuleCount();
    const quiet = 4;
    const totalModules = moduleCount + quiet * 2;
    const scale = Math.floor(canvas.width / totalModules);
    const drawnSize = scale * totalModules;
    const offset = Math.floor((canvas.width - drawnSize) / 2);

    ctx.imageSmoothingEnabled = false;
    ctx.fillStyle = "#ffffff";
    ctx.fillRect(0, 0, canvas.width, canvas.height);
    ctx.fillStyle = "#000000";

    for (let row = 0; row < moduleCount; row += 1) {
      for (let col = 0; col < moduleCount; col += 1) {
        if (qr.isDark(row, col)) {
          ctx.fillRect(
            offset + (col + quiet) * scale,
            offset + (row + quiet) * scale,
            scale,
            scale);
        }
      }
    }
  }

  window.RemoteTouchpadQr = { draw: drawQr };
})();
