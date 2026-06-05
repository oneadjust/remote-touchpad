const loginError = document.getElementById("loginError");
const loginQrCanvas = document.getElementById("loginQrCanvas");
const loginQrText = document.getElementById("loginQrText");
const copyAddressButton = document.getElementById("copyAddressButton");
const params = new URLSearchParams(window.location.search);

let currentAccessUrl = location.origin;

if (params.get("loginError") === "1") {
  loginError.textContent = "\u5bc6\u7801\u4e0d\u6b63\u786e\uff0c\u8bf7\u91cd\u65b0\u8f93\u5165\u3002";
  loginError.hidden = false;
} else if (params.get("loginError") === "auth") {
  loginError.textContent = "\u8bf7\u5148\u767b\u5f55\u540e\u518d\u8bbf\u95ee\u89e6\u63a7\u677f\u3002";
  loginError.hidden = false;
}

async function loadAccessQr() {
  try {
    const response = await fetch("/api/info", { cache: "no-store" });
    const info = await response.json();
    currentAccessUrl = info.primaryAccessUrl || location.origin;
    loginQrText.textContent = currentAccessUrl;
    window.RemoteTouchpadQr.draw(loginQrCanvas, currentAccessUrl);
  } catch {
    currentAccessUrl = location.origin;
    loginQrText.textContent = currentAccessUrl;
    window.RemoteTouchpadQr.draw(loginQrCanvas, currentAccessUrl);
  }
}

copyAddressButton.addEventListener("click", async () => {
  try {
    await navigator.clipboard.writeText(currentAccessUrl);
    copyAddressButton.textContent = "\u5df2\u590d\u5236";
    setTimeout(() => {
      copyAddressButton.textContent = "\u590d\u5236\u5730\u5740";
    }, 1200);
  } catch {
    loginQrText.textContent = currentAccessUrl;
  }
});

loadAccessQr();
