using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using RemoteTouchpad.Input;

namespace RemoteTouchpad.Web;

public sealed class RemoteControlServer : IAsyncDisposable
{
    private const string SessionCookieName = "remoteTouchpadSession";
    private const string StaticAssetVersion = "20260605-v2-fix8";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly PathString[] ProtectedStaticPaths =
    [
        new("/control.html"),
        new("/control.js")
    ];

    private readonly AppConfig _config;
    private readonly ConfigStore _configStore;
    private readonly DesktopMagnifier _desktopMagnifier = new();
    private readonly KeyboardController _keyboardController = new();
    private readonly MouseController _mouseController = new();
    private WebApplication? _app;

    public RemoteControlServer(AppConfig config, ConfigStore configStore)
    {
        _config = config;
        _configStore = configStore;
    }

    public int Port => _config.Port;

    public IReadOnlyList<string> AccessUrls => AccessUrlProvider.GetAccessUrls(Port);

    public string PrimaryAccessUrl => AccessUrlProvider.GetPrimaryAccessUrl(Port);

    public string LocalQrPageUrl => AccessUrlProvider.GetLocalQrPageUrl(Port);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        AppLogger.Info($"Preparing web server. ListenUrl=http://*:{_config.Port}");

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls($"http://*:{_config.Port}");

        _app = builder.Build();
        _app.Use(RequestLoggingMiddleware);
        _app.Use(ProtectedStaticMiddleware);
        _app.UseWebSockets();

        var webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        AppLogger.Info($"Static web root. Path={webRoot}, Exists={Directory.Exists(webRoot)}");

        if (Directory.Exists(webRoot))
        {
            _app.UseDefaultFiles(new DefaultFilesOptions
            {
                FileProvider = new PhysicalFileProvider(webRoot)
            });
            _app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(webRoot),
                OnPrepareResponse = context =>
                {
                    context.Context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
                    context.Context.Response.Headers.Pragma = "no-cache";
                    context.Context.Response.Headers.Expires = "0";
                    AppLogger.Info($"Static file served. Path={context.Context.Request.Path}, File={context.File.Name}");
                }
            });
        }

        _app.MapGet("/api/info", (HttpContext context) =>
        {
            AppLogger.Info("API info requested.");
            return Results.Json(new
            {
                name = "RemoteTouchpad",
                port = _config.Port,
                authenticated = IsAuthorized(context),
                logPath = AppLogger.LogPath,
                sensitivity = _config.Sensitivity,
                magnifierZoom = _config.MagnifierZoom,
                magnifierSize = _config.MagnifierSize,
                mediaBindings = new
                {
                    playPause = MediaBinding.GetDisplayName(_config.MediaBindings.PlayPause),
                    previous = MediaBinding.GetDisplayName(_config.MediaBindings.Previous),
                    next = MediaBinding.GetDisplayName(_config.MediaBindings.Next),
                    volumeDown = MediaBinding.GetDisplayName(_config.MediaBindings.VolumeDown),
                    volumeUp = MediaBinding.GetDisplayName(_config.MediaBindings.VolumeUp)
                },
                appVersion = StaticAssetVersion,
                staticAssetVersion = StaticAssetVersion,
                accessUrls = AccessUrls,
                primaryAccessUrl = PrimaryAccessUrl
            });
        });

        _app.MapGet("/logs", (HttpContext context) =>
        {
            AppLogger.Info("Logs page requested.");
            if (!IsAuthorized(context) && !IsLocalRequest(context))
            {
                AppLogger.Info("Logs page rejected. Not authorized and not local.");
                return Results.Unauthorized();
            }

            if (!File.Exists(AppLogger.LogPath))
            {
                return Results.Text("\u65e5\u5fd7\u6587\u4ef6\u5c1a\u672a\u521b\u5efa\u3002", "text/plain; charset=utf-8");
            }

            var lines = File.ReadLines(AppLogger.LogPath).TakeLast(300);
            return Results.Text(string.Join(Environment.NewLine, lines), "text/plain; charset=utf-8");
        });

        _app.MapPost("/api/login", async (HttpContext context) =>
        {
            AppLogger.Info("API login requested.");
            var login = await JsonSerializer.DeserializeAsync<LoginRequest>(
                context.Request.Body,
                JsonOptions,
                cancellationToken: context.RequestAborted);

            if (login is null || login.Password != _config.Password)
            {
                AppLogger.Info($"API login failed. BodyNull={login is null}, PasswordLength={login?.Password?.Length ?? 0}");
                return Results.Unauthorized();
            }

            SetSessionCookie(context);
            AppLogger.Info($"API login success. PasswordLength={login.Password.Length}, SessionTokenLength={_config.SessionToken.Length}");
            return Results.Json(new LoginResponse(true), JsonOptions);
        });

        _app.MapPost("/login", async (HttpContext context) =>
        {
            AppLogger.Info("Form login requested.");
            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            var password = form["password"].ToString();
            AppLogger.Info($"Form login parsed. HasPassword={form.ContainsKey("password")}, PasswordLength={password.Length}");

            if (password != _config.Password)
            {
                AppLogger.Info("Form login failed. Redirecting to login error.");
                return Results.Redirect("/?loginError=1");
            }

            SetSessionCookie(context);
            AppLogger.Info($"Form login success. Redirecting to control page. SessionTokenLength={_config.SessionToken.Length}");
            return Results.Redirect("/control.html");
        });

        _app.MapGet("/logout", (HttpContext context) =>
        {
            ClearSessionCookie(context);
            AppLogger.Info("Logout requested. Session cookie cleared.");
            return Results.Redirect("/");
        });

        _app.Map("/ws", HandleWebSocketAsync);

        await _app.StartAsync(cancellationToken);
        AppLogger.Info($"Web server listening. Port={_config.Port}");
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            AppLogger.Info("Stopping web server.");
            await _app.StopAsync(TimeSpan.FromSeconds(2));
            await _app.DisposeAsync();
            AppLogger.Info("Web server stopped.");
        }
    }

    private async Task ProtectedStaticMiddleware(HttpContext context, Func<Task> next)
    {
        if (ProtectedStaticPaths.Any(path => context.Request.Path.Equals(path, StringComparison.OrdinalIgnoreCase))
            && !IsAuthorized(context))
        {
            AppLogger.Info($"Protected static rejected. Path={context.Request.Path}");
            context.Response.Redirect("/?loginError=auth");
            return;
        }

        await next();
    }

    private static async Task RequestLoggingMiddleware(HttpContext context, Func<Task> next)
    {
        var stopwatch = Stopwatch.StartNew();
        var remote = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        AppLogger.Info($"HTTP start {context.Request.Method} {DescribeRequestTarget(context)} Remote={remote}");

        try
        {
            await next();
            stopwatch.Stop();
            AppLogger.Info($"HTTP end {context.Request.Method} {context.Request.Path} Status={context.Response.StatusCode} ElapsedMs={stopwatch.ElapsedMilliseconds}");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            AppLogger.Error($"HTTP exception {context.Request.Method} {context.Request.Path} ElapsedMs={stopwatch.ElapsedMilliseconds}", ex);
            throw;
        }
    }

    private async Task HandleWebSocketAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            AppLogger.Info("WebSocket rejected. Not a WebSocket request.");
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        if (!IsAuthorized(context))
        {
            AppLogger.Info("WebSocket rejected. Missing or invalid session cookie.");
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        AppLogger.Info("WebSocket accepted.");
        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var buffer = new byte[4096];

        try
        {
            while (socket.State == System.Net.WebSockets.WebSocketState.Open && !context.RequestAborted.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(buffer, context.RequestAborted);
                if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Close)
                {
                    AppLogger.Info("WebSocket close requested by client.");
                    break;
                }

                if (result.MessageType != System.Net.WebSockets.WebSocketMessageType.Text)
                {
                    AppLogger.Info($"WebSocket ignored non-text message. Type={result.MessageType}, Bytes={result.Count}");
                    continue;
                }

                var payload = buffer.Take(result.Count).ToArray();
                AppLogger.Info($"WebSocket message received. Bytes={payload.Length}");
                HandleCommand(payload);
            }
        }
        catch (OperationCanceledException)
        {
            AppLogger.Info("WebSocket closed because the client disconnected.");
        }
        catch (System.Net.WebSockets.WebSocketException ex)
        {
            AppLogger.Info($"WebSocket closed with socket error. Message={ex.Message}");
        }
        catch (IOException ex)
        {
            AppLogger.Info($"WebSocket closed with IO error. Message={ex.Message}");
        }

        AppLogger.Info($"WebSocket loop ended. State={socket.State}");
    }

    private void HandleCommand(byte[] payload)
    {
        try
        {
            var command = JsonSerializer.Deserialize<ControlCommand>(payload, JsonOptions);
            if (command is null)
            {
                AppLogger.Info("Control command ignored. Command was null.");
                return;
            }

            switch (command.Type)
            {
                case "move":
                    AppLogger.Info($"Command move. Dx={command.Dx}, Dy={command.Dy}, Sensitivity={_config.Sensitivity}");
                    _mouseController.Move(command.Dx, command.Dy, _config.Sensitivity);
                    break;
                case "scroll":
                    AppLogger.Info($"Command scroll. Delta={command.Delta}");
                    _mouseController.Scroll(command.Delta);
                    break;
                case "button":
                    AppLogger.Info($"Command button. Button={command.Button}, Action={command.Action}");
                    HandleButtonCommand(command);
                    break;
                case "media":
                    AppLogger.Info($"Command media. Action={command.Action}");
                    HandleMediaCommand(command);
                    break;
                case "magnifier":
                    AppLogger.Info($"Command magnifier. Action={command.Action}");
                    HandleMagnifierCommand(command);
                    break;
                case "settings":
                    AppLogger.Info($"Command settings. Sensitivity={command.Sensitivity}");
                    HandleSettingsCommand(command);
                    break;
                default:
                    AppLogger.Info($"Unknown command type. Type={command.Type}");
                    break;
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to handle control command.", ex);
        }
    }

    private void HandleMediaCommand(ControlCommand command)
    {
        var binding = command.Action switch
        {
            "previous" => _config.MediaBindings.Previous,
            "next" => _config.MediaBindings.Next,
            "volumeDown" => _config.MediaBindings.VolumeDown,
            "volumeUp" => _config.MediaBindings.VolumeUp,
            _ => _config.MediaBindings.PlayPause
        };

        AppLogger.Info($"Command media executing. Action={command.Action}, Binding={binding}");
        _keyboardController.ExecuteBinding(binding, command.Action ?? "media");
    }

    private void HandleMagnifierCommand(ControlCommand command)
    {
        if (string.Equals(command.Action, "on", StringComparison.OrdinalIgnoreCase))
        {
            _desktopMagnifier.Start();
            _desktopMagnifier.Configure(_config.MagnifierZoom, _config.MagnifierSize);
            AppLogger.Info($"Desktop magnifier on requested. Running={_desktopMagnifier.IsRunning}");
            return;
        }

        if (string.Equals(command.Action, "off", StringComparison.OrdinalIgnoreCase))
        {
            _desktopMagnifier.Stop();
            AppLogger.Info("Desktop magnifier stopped.");
            return;
        }

        _desktopMagnifier.Toggle();
        AppLogger.Info($"Desktop magnifier toggled. Running={_desktopMagnifier.IsRunning}");
    }

    private void HandleSettingsCommand(ControlCommand command)
    {
        var changed = false;
        if (command.Sensitivity is >= 0.4 and <= 3.0)
        {
            _config.Sensitivity = Math.Round(command.Sensitivity, 2);
            AppLogger.Info($"Sensitivity saved. Value={_config.Sensitivity}");
            changed = true;
        }
        else if (command.Sensitivity != 0)
        {
            AppLogger.Info($"Sensitivity ignored. OutOfRange={command.Sensitivity}");
        }

        if (command.MagnifierZoom is >= 1.25 and <= 4.0)
        {
            _config.MagnifierZoom = Math.Round(command.MagnifierZoom, 2);
            AppLogger.Info($"Magnifier zoom saved. Value={_config.MagnifierZoom}");
            changed = true;
        }
        else if (command.MagnifierZoom != 0)
        {
            AppLogger.Info($"Magnifier zoom ignored. OutOfRange={command.MagnifierZoom}");
        }

        if (command.MagnifierSize is >= 160 and <= 420)
        {
            _config.MagnifierSize = command.MagnifierSize;
            AppLogger.Info($"Magnifier size saved. Value={_config.MagnifierSize}");
            changed = true;
        }
        else if (command.MagnifierSize != 0)
        {
            AppLogger.Info($"Magnifier size ignored. OutOfRange={command.MagnifierSize}");
        }

        if (changed)
        {
            _configStore.Save(_config);
            _desktopMagnifier.Configure(_config.MagnifierZoom, _config.MagnifierSize);
        }
    }

    private void HandleButtonCommand(ControlCommand command)
    {
        var button = string.Equals(command.Button, "right", StringComparison.OrdinalIgnoreCase)
            ? MouseButton.Right
            : MouseButton.Left;

        switch (command.Action)
        {
            case "down":
                _mouseController.Down(button);
                break;
            case "up":
                _mouseController.Up(button);
                break;
            case "double":
                _mouseController.DoubleClick(button);
                break;
            default:
                _mouseController.Click(button);
                break;
        }
    }

    private bool IsAuthorized(HttpContext context)
    {
        return context.Request.Cookies.TryGetValue(SessionCookieName, out var session)
            && TimeSafeEquals(session, _config.SessionToken);
    }

    private void SetSessionCookie(HttpContext context)
    {
        context.Response.Cookies.Append(
            SessionCookieName,
            _config.SessionToken,
            new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                IsEssential = true
            });
    }

    private static void ClearSessionCookie(HttpContext context)
    {
        context.Response.Cookies.Delete(
            SessionCookieName,
            new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax
            });
    }

    private static bool TimeSafeEquals(string left, string right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        var diff = 0;
        for (var i = 0; i < left.Length; i++)
        {
            diff |= left[i] ^ right[i];
        }

        return diff == 0;
    }

    private static bool IsLocalRequest(HttpContext context)
    {
        var remote = context.Connection.RemoteIpAddress;
        return remote is not null && (IPAddress.IsLoopback(remote) || remote.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(remote.MapToIPv4()));
    }

    private static string DescribeRequestTarget(HttpContext context)
    {
        var path = context.Request.Path.ToString();
        var query = context.Request.QueryString.ToString();
        if (string.IsNullOrEmpty(query))
        {
            return path;
        }

        return context.Request.Query.ContainsKey("token")
            ? $"{path}?token=<redacted:length={context.Request.Query["token"].ToString().Length}>"
            : $"{path}{query}";
    }

    private sealed record LoginRequest(string Password);
    private sealed record LoginResponse(bool Ok);

    private sealed class ControlCommand
    {
        public string? Type { get; set; }
        public double Dx { get; set; }
        public double Dy { get; set; }
        public double Delta { get; set; }
        public double Sensitivity { get; set; }
        public double MagnifierZoom { get; set; }
        public int MagnifierSize { get; set; }
        public string? Button { get; set; }
        public string? Action { get; set; }
    }
}
