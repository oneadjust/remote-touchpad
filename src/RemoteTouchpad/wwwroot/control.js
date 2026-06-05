const connectionState = document.getElementById("connectionState");
const accessUrl = document.getElementById("accessUrl");
const pad = document.getElementById("pad");
const leftButton = document.getElementById("leftButton");
const rightButton = document.getElementById("rightButton");
const previousButton = document.getElementById("previousButton");
const playPauseButton = document.getElementById("playPauseButton");
const nextButton = document.getElementById("nextButton");
const volumeDownButton = document.getElementById("volumeDownButton");
const volumeUpButton = document.getElementById("volumeUpButton");
const sensitivityRange = document.getElementById("sensitivityRange");
const sensitivityValue = document.getElementById("sensitivityValue");
const magnifierToggleButton = document.getElementById("magnifierToggleButton");
const magnifierZoomRange = document.getElementById("magnifierZoomRange");
const magnifierZoomValue = document.getElementById("magnifierZoomValue");
const magnifierSizeRange = document.getElementById("magnifierSizeRange");
const magnifierSizeValue = document.getElementById("magnifierSizeValue");
const mediaBindingText = document.getElementById("mediaBindingText");
const qrCanvas = document.getElementById("qrCanvas");
const qrText = document.getElementById("qrText");
const versionText = document.getElementById("versionText");

let socket = null;
let reconnectTimer = null;
let settingsTimer = null;
let magnifierOn = false;
let lastTap = 0;
const pointers = new Map();

function setState(text) {
  connectionState.textContent = text;
}

function connect() {
  clearTimeout(reconnectTimer);
  const protocol = location.protocol === "https:" ? "wss" : "ws";
  socket = new WebSocket(`${protocol}://${location.host}/ws`);
  let opened = false;

  socket.addEventListener("open", () => {
    opened = true;
    setState("\u5df2\u8fde\u63a5");
  });

  socket.addEventListener("close", () => {
    setState("\u5df2\u65ad\u5f00");
    if (!opened) {
      window.location.href = "/?loginError=auth";
      return;
    }

    reconnectTimer = setTimeout(connect, 1200);
  });

  socket.addEventListener("error", () => setState("\u8fde\u63a5\u9519\u8bef"));
}

function send(message) {
  if (socket && socket.readyState === WebSocket.OPEN) {
    socket.send(JSON.stringify(message));
  }
}

function sendButton(button, action = "click") {
  send({ type: "button", button, action });
}

function sendMedia(action) {
  send({ type: "media", action });
}

function sendMagnifier(action) {
  send({ type: "magnifier", action });
}

function updateSensitivityLabel(value) {
  sensitivityValue.textContent = `${Number(value).toFixed(2)}x`;
}

function updateMagnifierLabels() {
  magnifierZoomValue.textContent = `${Number(magnifierZoomRange.value).toFixed(2)}x`;
  magnifierSizeValue.textContent = `${Number(magnifierSizeRange.value).toFixed(0)}px`;
}

function scheduleSettingsSave() {
  clearTimeout(settingsTimer);
  settingsTimer = setTimeout(() => {
    send({
      type: "settings",
      sensitivity: Number(sensitivityRange.value),
      magnifierZoom: Number(magnifierZoomRange.value),
      magnifierSize: Number(magnifierSizeRange.value)
    });
  }, 120);
}

function setupTabs() {
  const tabs = [...document.querySelectorAll(".tab")];
  const panels = [...document.querySelectorAll(".tab-panel")];

  tabs.forEach((tab) => {
    tab.addEventListener("click", () => {
      const target = tab.dataset.tab;
      tabs.forEach((item) => {
        const active = item === tab;
        item.classList.toggle("is-active", active);
        item.setAttribute("aria-selected", String(active));
      });

      panels.forEach((panel) => {
        const active = panel.dataset.panel === target;
        panel.hidden = !active;
        panel.classList.toggle("is-active", active);
      });
    });
  });
}

async function loadInfo() {
  try {
    const response = await fetch("/api/info", { cache: "no-store" });
    const info = await response.json();
    const primaryUrl = info.primaryAccessUrl || location.origin;
    const currentSensitivity = Number(info.sensitivity || 1);
    const magnifierZoom = Number(info.magnifierZoom || 2);
    const magnifierSize = Number(info.magnifierSize || 260);
    accessUrl.textContent = primaryUrl;
    sensitivityRange.value = String(currentSensitivity);
    magnifierZoomRange.value = String(magnifierZoom);
    magnifierSizeRange.value = String(magnifierSize);
    updateSensitivityLabel(currentSensitivity);
    updateMagnifierLabels();
    updateMediaBindingText(info.mediaBindings || {});
    window.RemoteTouchpadQr.draw(qrCanvas, primaryUrl);
    qrText.textContent = primaryUrl;
    versionText.textContent = `\u7248\u672c ${info.appVersion || "unknown"} · ${primaryUrl}`;
  } catch {
    accessUrl.textContent = location.origin;
    updateMediaBindingText({});
    window.RemoteTouchpadQr.draw(qrCanvas, location.origin);
    qrText.textContent = location.origin;
    versionText.textContent = "\u65e0\u6cd5\u83b7\u53d6\u7248\u672c\u4fe1\u606f";
  }
}

function updateMediaBindingText(bindings) {
  mediaBindingText.textContent = `\u64ad\u653e/\u6682\u505c: ${bindings.playPause || "-"} · \u4e0a\u4e00\u66f2: ${bindings.previous || "-"} · \u4e0b\u4e00\u66f2: ${bindings.next || "-"} · \u97f3\u91cf\u51cf: ${bindings.volumeDown || "-"} · \u97f3\u91cf\u52a0: ${bindings.volumeUp || "-"}`;
}

function toggleMagnifier() {
  magnifierOn = !magnifierOn;
  magnifierToggleButton.textContent = magnifierOn ? "\u5173\u95ed\u684c\u9762\u653e\u5927\u955c" : "\u5f00\u542f\u684c\u9762\u653e\u5927\u955c";
  sendMagnifier(magnifierOn ? "on" : "off");
}

pad.addEventListener("contextmenu", (event) => event.preventDefault());

pad.addEventListener("pointerdown", (event) => {
  pad.setPointerCapture(event.pointerId);
  pointers.set(event.pointerId, { x: event.clientX, y: event.clientY, moved: 0 });
});

pad.addEventListener("pointermove", (event) => {
  const previous = pointers.get(event.pointerId);
  if (!previous) {
    return;
  }

  if (pointers.size === 1) {
    const dx = event.clientX - previous.x;
    const dy = event.clientY - previous.y;
    send({ type: "move", dx, dy });
    previous.moved += Math.abs(dx) + Math.abs(dy);
  } else if (pointers.size === 2) {
    const dy = event.clientY - previous.y;
    send({ type: "scroll", delta: dy * 18 });
    previous.moved += Math.abs(dy);
  }

  pointers.set(event.pointerId, { x: event.clientX, y: event.clientY, moved: previous.moved });
});

pad.addEventListener("pointerup", (event) => {
  const previous = pointers.get(event.pointerId);
  pointers.delete(event.pointerId);

  const now = Date.now();
  const moved = previous ? previous.moved : 0;
  if (moved < 4 && now - lastTap < 260) {
    sendButton("left", "double");
    lastTap = 0;
  } else if (moved < 4) {
    sendButton("left");
    lastTap = now;
  }
});

pad.addEventListener("pointercancel", (event) => {
  pointers.delete(event.pointerId);
});

leftButton.addEventListener("click", () => sendButton("left"));
rightButton.addEventListener("click", () => sendButton("right"));
previousButton.addEventListener("click", () => sendMedia("previous"));
playPauseButton.addEventListener("click", () => sendMedia("playPause"));
nextButton.addEventListener("click", () => sendMedia("next"));
volumeDownButton.addEventListener("click", () => sendMedia("volumeDown"));
volumeUpButton.addEventListener("click", () => sendMedia("volumeUp"));
magnifierToggleButton.addEventListener("click", toggleMagnifier);

sensitivityRange.addEventListener("input", () => {
  updateSensitivityLabel(sensitivityRange.value);
  scheduleSettingsSave();
});

magnifierZoomRange.addEventListener("input", () => {
  updateMagnifierLabels();
  scheduleSettingsSave();
});

magnifierSizeRange.addEventListener("input", () => {
  updateMagnifierLabels();
  scheduleSettingsSave();
});

document.addEventListener("visibilitychange", () => {
  if (!document.hidden && (!socket || socket.readyState === WebSocket.CLOSED)) {
    connect();
  }
});

setupTabs();
loadInfo();
connect();
