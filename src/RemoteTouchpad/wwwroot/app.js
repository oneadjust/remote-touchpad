const tokenKey = "remoteTouchpadToken";
const loginView = document.getElementById("login");
const controllerView = document.getElementById("controller");
const loginForm = document.getElementById("loginForm");
const connectButton = document.getElementById("connectButton");
const passwordInput = document.getElementById("password");
const loginError = document.getElementById("loginError");
const connectionState = document.getElementById("connectionState");
const pad = document.getElementById("pad");
const leftButton = document.getElementById("leftButton");
const rightButton = document.getElementById("rightButton");

let socket = null;
let reconnectTimer = null;
let lastTap = 0;
const pointers = new Map();

function showController() {
  loginView.hidden = true;
  controllerView.hidden = false;
}

function showLogin() {
  controllerView.hidden = true;
  loginView.hidden = false;
}

function setState(text) {
  connectionState.textContent = text;
}

function markNativeLoginStarted() {
  showLoginMessage("Connecting...");
  setLoginBusy(true);
}

function showLoginMessage(message) {
  loginError.textContent = message;
  loginError.hidden = false;
}

function saveToken(token) {
  localStorage.setItem(tokenKey, token);
  document.cookie = `${tokenKey}=${encodeURIComponent(token)}; path=/; SameSite=Lax`;
}

function readCookieToken() {
  const prefix = `${tokenKey}=`;
  return document.cookie
    .split(";")
    .map((part) => part.trim())
    .find((part) => part.startsWith(prefix))
    ?.slice(prefix.length);
}

function setLoginBusy(isBusy) {
  connectButton.disabled = isBusy;
  connectButton.textContent = isBusy ? "Connecting..." : "Connect";
}

function connect(token) {
  if (!token) {
    showLogin();
    return;
  }

  clearTimeout(reconnectTimer);
  const protocol = location.protocol === "https:" ? "wss" : "ws";
  socket = new WebSocket(`${protocol}://${location.host}/ws?token=${encodeURIComponent(token)}`);
  let opened = false;

  socket.addEventListener("open", () => {
    opened = true;
    setState("Connected");
  });

  socket.addEventListener("close", () => {
    setState("Disconnected");
    if (!opened) {
      localStorage.removeItem(tokenKey);
      showLogin();
      return;
    }

    reconnectTimer = setTimeout(() => connect(localStorage.getItem(tokenKey)), 1200);
  });

  socket.addEventListener("error", () => setState("Connection error"));
}

function send(message) {
  if (socket && socket.readyState === WebSocket.OPEN) {
    socket.send(JSON.stringify(message));
  }
}

function sendButton(button, action = "click") {
  send({ type: "button", button, action });
}

function startWithStoredToken() {
  const url = new URL(window.location.href);
  const urlToken = url.searchParams.get("token");
  const loginErrorFlag = url.searchParams.get("loginError");

  if (urlToken) {
    saveToken(urlToken);
    url.searchParams.delete("token");
    window.history.replaceState({}, "", url.pathname);
  }

  if (loginErrorFlag) {
    showLoginMessage("Wrong password. Please try again.");
  }

  const token = urlToken || localStorage.getItem(tokenKey) || readCookieToken();
  if (!token) {
    showLogin();
    return;
  }

  showController();
  connect(token);
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

document.addEventListener("visibilitychange", () => {
  if (!document.hidden && (!socket || socket.readyState === WebSocket.CLOSED)) {
    const token = localStorage.getItem(tokenKey);
    if (token) {
      connect(token);
    }
  }
});

startWithStoredToken();
