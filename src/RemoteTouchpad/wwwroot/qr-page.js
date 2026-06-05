const qrCanvas = document.getElementById("qrCanvas");
const qrText = document.getElementById("qrText");
const copyQrAddressButton = document.getElementById("copyQrAddressButton");

let currentQrUrl = location.origin;

async function loadQr() {
  try {
    const response = await fetch("/api/info", { cache: "no-store" });
    const info = await response.json();
    currentQrUrl = info.primaryAccessUrl || location.origin;
    qrText.textContent = currentQrUrl;
    window.RemoteTouchpadQr.draw(qrCanvas, currentQrUrl);
  } catch {
    currentQrUrl = location.origin;
    qrText.textContent = currentQrUrl;
    window.RemoteTouchpadQr.draw(qrCanvas, currentQrUrl);
  }
}

copyQrAddressButton.addEventListener("click", async () => {
  try {
    await navigator.clipboard.writeText(currentQrUrl);
    copyQrAddressButton.textContent = "\u5df2\u590d\u5236";
    setTimeout(() => {
      copyQrAddressButton.textContent = "\u590d\u5236\u5730\u5740";
    }, 1200);
  } catch {
    qrText.textContent = currentQrUrl;
  }
});

loadQr();
