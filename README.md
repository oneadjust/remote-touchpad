# Remote Touchpad

Remote Touchpad 是一个 Windows 局域网 Web 触控板工具。电脑端运行托盘程序后，手机、iPad 或同网段浏览器访问电脑地址，即可通过网页控制鼠标移动、点击和滚轮。

这个项目的目标很简单：躺在床上看视频、投屏、演示或远距离操作电脑时，不需要投屏画面，只把手机当作轻量鼠标使用。

## 功能

- Windows 托盘常驻程序。
- 内置 Kestrel Web 服务，默认端口 `8765`。
- 手机/iPad 浏览器触控板页面。
- 单指移动鼠标、点击、双击、双指滚动。
- 页面底部提供左键、右键按钮。
- 固定密码登录，默认密码 `123456`。
- 登录后使用 `HttpOnly` 会话 Cookie，控制页和 WebSocket 都由后端校验。
- 托盘菜单支持显示地址、复制地址、复制日志路径、修改密码、退出。
- 后端请求和 WebSocket 日志，便于排障。
- 透明底白色 1:1 托盘图标，适配 Windows 系统托盘。

## 快速开始

运行开发版：

```powershell
dotnet run --project src/RemoteTouchpad/RemoteTouchpad.csproj
```

启动后，右键系统托盘图标，选择“显示访问地址”或“复制访问地址”。在同一局域网内，用手机或 iPad 浏览器打开该地址。

常用入口：

```text
http://localhost:8765/
http://localhost:8765/control.html
http://localhost:8765/logs
```

首次登录使用默认密码：

```text
123456
```

建议首次运行后通过托盘菜单修改密码。

## 发布

生成 Windows x64 发布目录：

```powershell
dotnet publish src\RemoteTouchpad\RemoteTouchpad.csproj -c Release -r win-x64 --self-contained false
```

发布输出目录：

```text
src\RemoteTouchpad\bin\Release\net8.0-windows\win-x64\publish
```

如果覆盖已有发布目录，请先退出正在运行的 `RemoteTouchpad.exe`，否则 Windows 可能锁定 DLL 文件。

## 配置与日志

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
- 密码：`123456`
- 鼠标灵敏度：`1.0`

## 安全说明

Remote Touchpad 面向个人电脑和同一局域网使用，不包含外网穿透能力，也不传输桌面画面。

当前登录流程：

```text
POST /login -> Set-Cookie remoteTouchpadSession=<HttpOnly> -> 302 /control.html
```

控制页通过会话 Cookie 建立 WebSocket：

```text
GET /ws
```

`/control.html`、`/control.js` 和 `/ws` 都需要有效会话。`/logs` 仅允许已登录访问，或从本机访问。

## 常见问题

如果手机无法访问：

- 确认手机和电脑在同一局域网。
- 使用托盘菜单复制的局域网地址，不要在手机上使用 `localhost`。
- 检查 Windows 防火墙是否拦截端口 `8765`。
- 确认电脑端程序仍在运行。

如果登录或控制异常：

- 强制刷新浏览器页面。
- 打开 `http://localhost:8765/logs` 查看服务端日志。
- 检查 `%APPDATA%\RemoteTouchpad\config.json` 中的密码配置。

## 文档

- [用户手册](docs/user-guide.md)
- [实现复盘](docs/remote-touchpad-retrospective.md)

## 技术栈

- .NET 8
- C#
- Windows Forms 托盘程序
- ASP.NET Core Kestrel
- WebSocket
- Windows `SendInput`
