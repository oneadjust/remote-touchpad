# Remote Touchpad

Remote Touchpad is a Windows LAN remote touchpad. Run the tray app on a Windows PC, then use a phone, tablet, or another browser on the same network to control mouse movement, clicks, scrolling, media keys, volume, pointer sensitivity, and a desktop magnifier.

It does not stream or transmit your desktop image. The magnifier is drawn locally on the Windows computer.

## Features

- Windows tray app with a built-in Kestrel web server.
- Mobile-first web controller with three tabs: Touchpad, Media, Settings.
- Single-finger mouse movement, tap, double tap, two-finger scroll, left click, and right click.
- Media controls: previous, play/pause, next, volume down, and volume up.
- Configurable media key bindings from the Windows tray menu, including captured keyboard shortcuts.
- Pointer sensitivity slider, saved to user config.
- Desktop pointer magnifier with adjustable zoom and lens size.
- QR code connection page and QR code on the login page.
- Tray menu actions: show/copy access address, open QR page, copy log path, change password, configure media keys, startup toggle, and exit.
- Current-user startup option via `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.

## Direct Use From Release

This is the easiest path for most users.

1. Download `RemoteTouchpad-win-x64-v2.0.0.zip` from the GitHub Releases page.
2. Extract the zip to any folder, for example:

   ```text
   C:\Tools\RemoteTouchpad
   ```

3. Run `RemoteTouchpad.exe`.
4. Right-click the tray icon and choose `显示访问地址`, `复制访问地址`, or `打开二维码页面`.
5. On your phone or tablet, open the shown LAN address or scan the QR code.
6. Log in with the default password:

   ```text
   123456
   ```

7. After first launch, use the tray menu `修改密码` to set your own password.

Notes:

- The port is `8765` by default.
- The IP address is not fixed. It is the current LAN IP of the Windows PC, such as `192.168.1.4`.
- The program automatically prefers usable LAN addresses and filters out loopback, link-local, and virtual ranges such as `198.18.0.0/15`.
- If Windows Firewall asks for permission, allow LAN/private-network access.

## Build From Source

Requirements:

- Windows
- .NET 8 SDK

Run the development build:

```powershell
dotnet run --project src\RemoteTouchpad\RemoteTouchpad.csproj
```

Publish a Windows x64 release build:

```powershell
dotnet publish src\RemoteTouchpad\RemoteTouchpad.csproj -c Release -r win-x64 --self-contained false
```

Publish output:

```text
src\RemoteTouchpad\bin\Release\net8.0-windows\win-x64\publish
```

If replacing an existing publish folder, exit the running `RemoteTouchpad.exe` first. Windows may lock DLL files while the app is running.

## Useful URLs

After the app starts:

```text
http://<your-pc-lan-ip>:8765/
http://<your-pc-lan-ip>:8765/control.html
http://<your-pc-lan-ip>:8765/qr.html
http://localhost:8765/logs
```

`localhost` only works on the Windows PC itself. Phones must use the LAN IP shown by the tray menu or QR code.

## Configuration And Logs

Config file:

```text
%APPDATA%\RemoteTouchpad\config.json
```

Log file:

```text
%APPDATA%\RemoteTouchpad\logs\remote-touchpad.log
```

Default config includes:

- Port: `8765`
- Password: `123456`
- Pointer sensitivity: `1.0`
- Magnifier zoom: `2.0`
- Magnifier lens size: `260`

## Security Notes

Remote Touchpad is designed for personal computers on the same LAN.

- It does not provide public-network tunneling.
- It does not transmit the desktop image.
- `/control.html`, `/control.js`, and `/ws` require a valid login session.
- `/qr.html` only displays the access URL and does not expose the password or session token.
- `/logs` is available to logged-in users or local requests.

Login flow:

```text
POST /login -> Set-Cookie remoteTouchpadSession=<HttpOnly> -> 302 /control.html
```

Control uses WebSocket:

```text
GET /ws
```

## Troubleshooting

If the phone cannot connect:

- Make sure the phone and PC are on the same LAN.
- Use the tray menu copied address or QR code; do not use `localhost` on the phone.
- Check whether Windows Firewall blocks port `8765`.
- Check that `RemoteTouchpad.exe` is still running.
- Open `http://localhost:8765/logs` on the PC to inspect server logs.

If QR scanning fails:

- Open `http://127.0.0.1:8765/qr.html` on the PC from the tray menu.
- Confirm the text under the QR code is a LAN address, for example `http://192.168.x.x:8765/`.
- Refresh the page to avoid cached static assets.

## Documentation

- [User guide](docs/user-guide.md)
- [Implementation retrospective](docs/remote-touchpad-retrospective.md)

## Technology

- .NET 8
- C#
- Windows Forms tray app
- ASP.NET Core Kestrel
- WebSocket
- Windows `SendInput`
- `qrcode-generator` for QR code rendering
