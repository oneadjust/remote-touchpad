using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AppConfig _config;
    private readonly MouseController _mouseController = new();
    private WebApplication? _app;

    public RemoteControlServer(AppConfig config)
    {
        _config = config;
    }

    public int Port => _config.Port;

    public IReadOnlyList<string> AccessUrls => GetLocalIpAddresses()
        .Select(address => $"http://{address}:{Port}")
        .Prepend($"http://127.0.0.1:{Port}")
        .ToArray();

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        AppLogger.Info($"Preparing web server. ListenUrl=http://*:{_config.Port}");

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls($"http://*:{_config.Port}");

        _app = builder.Build();
        _app.Use(RequestLoggingMiddleware);
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

        _app.MapGet("/api/info", () =>
        {
            AppLogger.Info("API info requested.");
            return Results.Json(new
            {
                name = "RemoteTouchpad",
                port = _config.Port,
                authenticated = false,
                logPath = AppLogger.LogPath
            });
        });

        _app.MapGet("/logs", () =>
        {
            AppLogger.Info("Logs page requested.");
            if (!File.Exists(AppLogger.LogPath))
            {
                return Results.Text("Log file does not exist yet.", "text/plain");
            }

            var lines = File.ReadLines(AppLogger.LogPath).TakeLast(300);
            return Results.Text(string.Join(Environment.NewLine, lines), "text/plain");
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

            AppLogger.Info($"API login success. PasswordLength={login.Password.Length}, TokenLength={_config.AuthToken.Length}");
            return Results.Json(new LoginResponse(_config.AuthToken), JsonOptions);
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

            context.Response.Cookies.Append(
                "remoteTouchpadToken",
                _config.AuthToken,
                new CookieOptions
                {
                    SameSite = SameSiteMode.Lax,
                    IsEssential = true
                });

            AppLogger.Info($"Form login success. Redirecting to control page. TokenLength={_config.AuthToken.Length}");
            return Results.Redirect($"/control.html?token={Uri.EscapeDataString(_config.AuthToken)}");
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

        var token = context.Request.Query["token"].ToString();
        if (!TimeSafeEquals(token, _config.AuthToken))
        {
            AppLogger.Info($"WebSocket rejected. Invalid token. TokenLength={token.Length}, ExpectedLength={_config.AuthToken.Length}");
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

    private static string DescribeRequestTarget(HttpContext context)
    {
        var path = context.Request.Path.ToString();
        var query = context.Request.QueryString.ToString();
        if (string.IsNullOrEmpty(query))
        {
            return path;
        }

        if (context.Request.Query.ContainsKey("token"))
        {
            return $"{path}?token=<redacted:length={context.Request.Query["token"].ToString().Length}>";
        }

        return $"{path}{query}";
    }

    private static IEnumerable<IPAddress> GetLocalIpAddresses()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up)
            .Where(adapter => adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(adapter => adapter.GetIPProperties().UnicastAddresses)
            .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(address => address.Address)
            .Where(address => !IPAddress.IsLoopback(address));
    }

    private sealed record LoginRequest(string Password);
    private sealed record LoginResponse(string Token);

    private sealed class ControlCommand
    {
        public string? Type { get; set; }
        public double Dx { get; set; }
        public double Dy { get; set; }
        public double Delta { get; set; }
        public string? Button { get; set; }
        public string? Action { get; set; }
    }
}
