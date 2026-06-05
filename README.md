# Remote Touchpad 远程触控板

Remote Touchpad 是一个 Windows 局域网远程触控板工具。你在 Windows 电脑上运行托盘程序，然后用同一局域网内的手机、平板或浏览器打开控制页，就可以控制鼠标移动、点击、滚动、媒体播放、音量、指针灵敏度和桌面放大镜。

项目不会传输电脑桌面画面。放大镜也只在 Windows 本机屏幕上绘制，不会把截图发送到手机端。

## 主要功能

- Windows 托盘程序，内置 Kestrel Web 服务。
- 手机优先的三 Tab 控制页：`触控板`、`媒体`、`设置`。
- 支持单指移动、点击、双击、双指滚动、左键、右键。
- 支持媒体控制：上一曲、播放/暂停、下一曲、音量减、音量加。
- 支持在 Windows 托盘菜单里配置媒体按键绑定，可以捕获键盘快捷键。
- 支持指针灵敏度调节，并保存到用户配置。
- 支持电脑屏幕上的跟随式圆形指针放大镜，可调节放大倍数和放大区域大小。
- 支持登录页二维码、独立二维码页面和控制页二维码。
- 托盘菜单支持显示/复制访问地址、打开二维码页面、复制日志路径、修改密码、配置媒体键位、开机自启、退出。
- 开机自启使用当前用户注册表启动项：`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`。

## 直接使用 Release

这是普通用户最推荐的方式，不需要安装 .NET SDK，也不需要自己编译代码。

1. 打开 GitHub Releases 页面，下载 `RemoteTouchpad-win-x64-v2.0.0.zip`。
2. 解压到任意目录，例如：

   ```text
   C:\Tools\RemoteTouchpad
   ```

3. 双击运行 `RemoteTouchpad.exe`。
4. 右键点击系统托盘里的 Remote Touchpad 图标。
5. 选择 `显示访问地址`、`复制访问地址` 或 `打开二维码页面`。
6. 在手机或平板上打开显示的局域网地址，或扫描二维码。
7. 使用默认密码登录：

   ```text
   123456
   ```

8. 首次使用后，建议在托盘菜单里选择 `修改密码`，设置自己的密码。

注意事项：

- 默认端口是 `8765`。
- 访问地址不是固定的，每个用户、每台电脑、每个网络环境都可能不同。
- 程序会优先选择可用的局域网地址，例如 `192.168.1.4`。
- 程序会过滤不可用或虚拟网段，例如 `127.x.x.x`、`169.254.x.x`、`198.18.0.0/15`。
- 如果 Windows 防火墙弹出提示，需要允许专用网络或局域网访问。

## 从源码编译

适合开发者或希望自行修改代码的用户。

环境要求：

- Windows
- .NET 8 SDK

运行开发版本：

```powershell
dotnet run --project src\RemoteTouchpad\RemoteTouchpad.csproj
```

发布 Windows x64 Release 版本：

```powershell
dotnet publish src\RemoteTouchpad\RemoteTouchpad.csproj -c Release -r win-x64 --self-contained false
```

发布输出目录：

```text
src\RemoteTouchpad\bin\Release\net8.0-windows\win-x64\publish
```

如果要覆盖已有发布目录，请先退出正在运行的 `RemoteTouchpad.exe`。程序运行时，Windows 可能会锁定 DLL 文件。

## 常用地址

程序启动后可以访问：

```text
http://<电脑局域网IP>:8765/
http://<电脑局域网IP>:8765/control.html
http://<电脑局域网IP>:8765/qr.html
http://localhost:8765/logs
```

`localhost` 只适用于 Windows 电脑本机。手机访问时必须使用托盘菜单或二维码里显示的局域网 IP。

## 配置和日志

配置文件：

```text
%APPDATA%\RemoteTouchpad\config.json
```

日志文件：

```text
%APPDATA%\RemoteTouchpad\logs\remote-touchpad.log
```

默认配置包括：

- 端口：`8765`
- 默认密码：`123456`
- 指针灵敏度：`1.0`
- 放大镜倍数：`2.0`
- 放大镜区域直径：`260`

## 安全说明

Remote Touchpad 主要面向个人电脑和同一局域网内的设备。

- 不提供公网穿透。
- 不传输电脑桌面画面。
- `/control.html`、`/control.js` 和 `/ws` 需要登录后才能访问。
- `/qr.html` 只显示访问地址，不包含密码或登录 token。
- `/logs` 仅允许已登录用户或本机请求访问。

登录流程：

```text
POST /login -> Set-Cookie remoteTouchpadSession=<HttpOnly> -> 302 /control.html
```

控制页通过 WebSocket 发送操作：

```text
GET /ws
```

## 常见问题

手机无法连接时，可以检查：

- 手机和电脑是否在同一个局域网。
- 手机是否使用了托盘菜单复制的地址或二维码地址，不要在手机上使用 `localhost`。
- Windows 防火墙是否阻止了 `8765` 端口。
- `RemoteTouchpad.exe` 是否仍在运行。
- 可以在电脑上打开 `http://localhost:8765/logs` 查看日志。

二维码无法扫描时，可以检查：

- 通过托盘菜单打开 `http://127.0.0.1:8765/qr.html`。
- 确认二维码下方文字是局域网地址，例如 `http://192.168.x.x:8765/`。
- 刷新页面，避免浏览器缓存旧的静态资源。

媒体控制无效时，可以尝试：

- 右键托盘图标，打开 `配置媒体键位`。
- 为播放、上一曲、下一曲、音量减、音量加分别捕获适合当前播放器的快捷键。
- 系统媒体键对部分播放器可能不生效，改用 `空格`、`K`、方向键或组合键通常更兼容。

## 文档

- [用户指南](docs/user-guide.md)
- [实现复盘](docs/remote-touchpad-retrospective.md)

## 技术栈

- .NET 8
- C#
- Windows Forms 托盘程序
- ASP.NET Core Kestrel
- WebSocket
- Windows `SendInput`
- `qrcode-generator` 二维码渲染
