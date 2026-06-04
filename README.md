# Remote Touchpad

Remote Touchpad is a Windows LAN web touchpad tool. Run the tray app on a Windows PC, then open the local web page from a phone, iPad, or browser on the same network to control the mouse.

## Current Behavior

- Windows tray app.
- Built-in Kestrel web server.
- Default local port: `8765`.
- Default password: `123456`.
- Login page: `http://localhost:8765/`.
- Touchpad page after login: `http://localhost:8765/control.html`.
- Server logs page: `http://localhost:8765/logs`.
- Log file: `%APPDATA%\RemoteTouchpad\logs\remote-touchpad.log`.
- Config file: `%APPDATA%\RemoteTouchpad\config.json`.

The current login flow intentionally uses a native HTML form and an HttpOnly session cookie:

```text
POST /login -> Set-Cookie remoteTouchpadSession=<HttpOnly> -> 302 /control.html
```

The control page then opens a WebSocket connection using that cookie:

```text
GET /ws
```

This avoids the earlier unreliable login-page UI switching flow.

## Features

- Single-finger mouse movement.
- Left click and right click buttons.
- Tap and double-tap support.
- Two-finger scrolling.
- Fixed password login.
- HttpOnly session-cookie WebSocket authentication.
- Detailed backend request and WebSocket logs.

## Run

```powershell
dotnet run --project src/RemoteTouchpad/RemoteTouchpad.csproj
```

After launch, use the tray icon to show or copy the access address.

## Publish

```powershell
dotnet publish src\RemoteTouchpad\RemoteTouchpad.csproj -c Release -r win-x64 --self-contained false
```

Publish output:

```text
src\RemoteTouchpad\bin\Release\net8.0-windows\win-x64\publish
```

Before publishing over an existing publish directory, stop any running `RemoteTouchpad.exe`; otherwise Windows may lock the DLL files.

## Troubleshooting

1. Confirm the running process path:

```powershell
Get-Process -Name RemoteTouchpad -ErrorAction SilentlyContinue | Select-Object Id,Path,StartTime
```

2. Confirm port `8765` is owned by the expected process:

```powershell
Get-NetTCPConnection -LocalPort 8765 -ErrorAction SilentlyContinue | Select-Object LocalAddress,LocalPort,State,OwningProcess
```

3. Open the log page:

```text
http://localhost:8765/logs
```

4. Check the log file directly:

```text
%APPDATA%\RemoteTouchpad\logs\remote-touchpad.log
```

5. Confirm the configured password:

```text
%APPDATA%\RemoteTouchpad\config.json
```

6. If the browser appears stale, force refresh `http://localhost:8765/`.

## Retrospective

See [docs/remote-touchpad-retrospective.md](docs/remote-touchpad-retrospective.md) for the implementation review, failure analysis, and lessons learned from the login debugging process.
