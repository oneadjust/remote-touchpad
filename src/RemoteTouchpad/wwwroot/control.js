const connectionState = document.getElementById("connectionState");
const pad = document.getElementById("pad");
const leftButton = document.getElementById("leftButton");
const rightButton = document.getElementById("rightButton");

let socket = null;
let reconnectTimer = null;
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
    connect();
  }
});

connect();
