# Remote Touchpad 复盘

## 背景

这个项目的目标是做一个 Windows 局域网 Web 触控板工具：电脑端运行托盘程序，手机或 iPad 访问局域网地址后，可以把浏览器页面当作鼠标触控板使用。第一版不做画面投递，只做鼠标移动、点击和滚轮。

当前最终架构：

- Windows 托盘程序负责启动和管理服务。
- Kestrel 内置 Web 服务监听 `8765` 端口。
- 登录页是 `http://localhost:8765/`。
- 登录成功后服务端设置 `HttpOnly` 会话 cookie，并跳转到独立触控板页 `/control.html`。
- 触控板页通过 `/ws` 建立 WebSocket，后端用会话 cookie 认证。
- 鼠标输入由 Windows `SendInput` 执行。
- 后端日志写入 `%APPDATA%\RemoteTouchpad\logs\remote-touchpad.log`，也可以通过 `/logs` 查看。

## 遇到的问题

### 1. HTML 和文案编码损坏

早期页面中中文文案出现乱码，甚至破坏了 HTML 闭合标签，例如 `</p>`、`</span>` 一类标签被写坏。结果是 DOM 结构不稳定，前端脚本绑定到的元素也不可靠。

处理方式：

- 重写 `index.html` 为干净 UTF-8/ASCII 文案。
- 避免在早期验证阶段引入容易被编码破坏的中文 UI 文案。

### 2. 静态资源缓存导致“修了但页面没变”

浏览器可能继续使用旧的 `app.js`。这导致代码已经发布，但用户页面仍旧运行旧脚本，看起来像“修复无效”。

处理方式：

- 静态资源加版本号，例如 `/app.js?v=20260604-7`。
- 服务端静态资源响应加 `Cache-Control: no-store, no-cache, must-revalidate`。

### 3. 验证环境和用户环境不一致

曾经主要验证了 `127.0.0.1` 和自动化理想路径，但用户实际打开的是 `http://localhost:8765/`。此外，测试有时使用 Debug 输出，而用户运行的是 Release publish 目录。

经验：

- 验证必须使用用户实际 URL：`http://localhost:8765/`。
- 验证必须使用发布目录中的实际 exe。
- 每次发布前必须确认旧进程已经停止，否则 publish 目录中的 DLL 可能被锁住。

### 4. 登录页内切换 UI 不稳

早期方案是登录页提交密码后，通过 JS 隐藏登录区域、显示触控板区域，再建立 WebSocket。在当前浏览器环境里，这条路径多次表现为“卡在 Connecting”或“不切换页面”。

最终修复：

- 登录页不再负责切换成触控板。
- `Connect` 使用原生 HTML 表单提交：`POST /login`。
- 后端验证密码成功后设置 `HttpOnly` 会话 cookie，并返回 `302 /control.html`。
- `control.html` 是独立触控板页面，只负责建立 WebSocket。
- `/control.html`、`/control.js`、`/ws` 都由后端校验会话 cookie。

这个方案更稳，因为登录成功后浏览器发生真实页面跳转，而不是依赖同页 JS 状态切换。

### 5. 缺少后端观测导致排障靠猜

早期排查主要依赖前端表现和本地请求验证，缺少后端日志。这样很难区分问题发生在浏览器、登录接口、重定向、静态资源、WebSocket 还是发布目录。

最终加入日志：

- 程序启动和配置加载。
- 静态资源路径和是否存在。
- 每个 HTTP 请求开始和结束。
- `/login` 表单是否收到、密码字段长度、登录成功或失败。
- 重定向目标。
- WebSocket token 校验结果和 accepted 状态。
- 鼠标控制命令。

日志入口：

```text
http://localhost:8765/logs
%APPDATA%\RemoteTouchpad\logs\remote-touchpad.log
```

## 最终登录链路

成功链路应该长这样：

```text
GET /
GET /styles.css?v=...
GET /app.js?v=...
POST /login
Set-Cookie remoteTouchpadSession=<HttpOnly>
302 /control.html
GET /control.html
GET /control.js?v=...
GET /ws
WebSocket accepted
```

如果登录失败，日志里应该看到：

```text
POST /login
Form login parsed. HasPassword=True, PasswordLength=<n>
Form login failed. Redirecting to login error.
302 /?loginError=1
```

## 排障顺序

以后遇到“不能登录”或“点了没反应”，按这个顺序排查：

1. 确认运行的是哪个 exe：

```powershell
Get-Process -Name RemoteTouchpad -ErrorAction SilentlyContinue | Select-Object Id,Path,StartTime
```

2. 确认端口归属：

```powershell
Get-NetTCPConnection -LocalPort 8765 -ErrorAction SilentlyContinue | Select-Object LocalAddress,LocalPort,State,OwningProcess
```

3. 打开日志页：

```text
http://localhost:8765/logs
```

4. 看是否出现 `POST /login`。

5. 如果没有 `POST /login`，问题在浏览器页面或表单提交。

6. 如果有 `POST /login` 但失败，看密码长度和配置文件：

```text
%APPDATA%\RemoteTouchpad\config.json
```

7. 如果登录成功但没进触控板，看是否有 `GET /control.html` 和 `GET /ws`。

8. 如果 WebSocket 被拒绝，看会话 cookie 是否存在，以及日志中的认证结果。

## 工程经验

- 不要只验证接口成功，还要验证用户真实流程。
- 不要只验证 Debug，要验证发布目录中的 exe。
- 前端问题不能只靠肉眼看页面，必须确认实际返回的 HTML/JS 版本。
- 内置浏览器、普通 Chrome、手机浏览器行为可能不同，登录这种关键路径应尽量使用浏览器原生能力。
- 对本地工具来说，日志不是锦上添花，而是排障基础设施。
- 遇到“用户说不行，但自动化通过”，优先怀疑验证场景不一致，而不是用户操作错误。

## 当前建议

后续继续迭代时，建议保持这个结构：

- 登录页只做登录。
- 控制页只做触控板。
- 后端负责认证和跳转。
- WebSocket 只处理控制事件。
- 每个关键路径都写日志。

如果未来要加功能，比如媒体快捷键、灵敏度配置、二维码配对，也应该继续保持“登录、控制、配置、日志”分离。
