# Remote Touchpad 用户手册

Remote Touchpad 用于在同一局域网内把手机或 iPad 变成 Windows 电脑的触控板。它不投屏、不传输桌面画面，只发送鼠标移动、点击和滚轮输入。

## 1. 启动 Windows 端

开发运行：

```powershell
dotnet run --project src/RemoteTouchpad/RemoteTouchpad.csproj
```

发布运行：

```powershell
dotnet publish src\RemoteTouchpad\RemoteTouchpad.csproj -c Release -r win-x64 --self-contained false
```

然后运行：

```text
src\RemoteTouchpad\bin\Release\net8.0-windows\win-x64\publish\RemoteTouchpad.exe
```

启动后，程序会出现在 Windows 系统托盘中。图标为透明底、白色线性触控板样式。

## 2. 获取访问地址

右键托盘图标，可以看到这些菜单：

- 显示访问地址
- 复制访问地址
- 复制日志路径
- 修改密码
- 退出

在电脑本机测试时可以打开：

```text
http://localhost:8765/
```

手机或 iPad 需要使用托盘中显示的局域网地址，例如：

```text
http://192.168.1.20:8765/
```

不要在手机上使用 `localhost`，因为它表示手机自己，不是电脑。

## 3. 登录

默认密码：

```text
123456
```

建议首次运行后通过托盘菜单“修改密码”改成自己的密码。配置保存在：

```text
%APPDATA%\RemoteTouchpad\config.json
```

登录成功后会跳转到：

```text
/control.html
```

URL 中不会出现 token。服务端会设置 `HttpOnly` 会话 Cookie，浏览器脚本无法读取该 Cookie。

## 4. 使用触控板

进入控制页后，状态显示“已连接”表示 WebSocket 已建立。

基础操作：

- 单指滑动：移动鼠标。
- 单击触控区域：左键单击。
- 快速双击触控区域：左键双击。
- 双指上下滑动：滚轮滚动。
- 底部“左键”：左键点击。
- 底部“右键”：右键点击。
- “退出登录”：清除会话并返回登录页。

典型使用场景：

- 看视频时切换下一集。
- 远距离暂停、播放、拖动进度条。
- 投屏或演示时临时控制电脑。
- 不方便拿鼠标时进行简单点击操作。

## 5. 日志与排障

日志页面：

```text
http://localhost:8765/logs
```

日志文件：

```text
%APPDATA%\RemoteTouchpad\logs\remote-touchpad.log
```

`/logs` 只允许已登录访问，或从电脑本机访问。

如果手机无法打开页面：

1. 确认手机和电脑连接同一个 Wi-Fi 或同一局域网。
2. 使用托盘菜单复制的局域网地址。
3. 确认电脑端 `RemoteTouchpad.exe` 正在运行。
4. 检查 Windows 防火墙是否拦截端口 `8765`。
5. 在电脑上打开 `http://localhost:8765/` 验证服务是否正常。

如果登录后无法进入控制页：

1. 确认密码是否和配置文件一致。
2. 强制刷新登录页。
3. 查看日志页面或日志文件。
4. 退出托盘程序后重新启动。

如果触控板状态不是“已连接”：

1. 刷新控制页。
2. 确认登录会话没有过期或被退出。
3. 查看日志中是否出现 `/ws` 认证失败或连接错误。

## 6. 当前限制

- 仅支持 Windows。
- 仅支持局域网 HTTP。
- 不支持外网远程控制。
- 不传输桌面画面。
- 第一版只做鼠标和滚轮控制，不包含键盘输入。

## 7. 开发说明

核心实现：

- Windows 托盘：Windows Forms `NotifyIcon`
- Web 服务：ASP.NET Core Kestrel
- 控制通道：WebSocket
- 鼠标输入：Windows `SendInput`
- 配置与日志：`%APPDATA%\RemoteTouchpad`

构建检查：

```powershell
dotnet build RemoteTouchpad.sln
```

前端脚本检查：

```powershell
node --check src\RemoteTouchpad\wwwroot\app.js
node --check src\RemoteTouchpad\wwwroot\control.js
```
