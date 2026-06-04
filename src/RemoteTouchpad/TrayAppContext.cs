using RemoteTouchpad.Web;

namespace RemoteTouchpad;

public sealed class TrayAppContext : ApplicationContext
{
    private readonly ConfigStore _configStore = new();
    private readonly AppConfig _config;
    private readonly NotifyIcon _notifyIcon;
    private readonly RemoteControlServer _server;

    public TrayAppContext()
    {
        AppLogger.Info($"Tray app starting. BaseDirectory={AppContext.BaseDirectory}");

        _config = _configStore.Load();
        AppLogger.Info($"Config loaded. Port={_config.Port}, PasswordLength={_config.Password.Length}, TokenLength={_config.AuthToken.Length}, LogPath={AppLogger.LogPath}");

        _server = new RemoteControlServer(_config);

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Remote Touchpad",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };

        _notifyIcon.DoubleClick += (_, _) => ShowAccessAddress();
        _ = StartServerAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _server.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        base.Dispose(disposing);
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Show access address", null, (_, _) => ShowAccessAddress());
        menu.Items.Add("Copy access address", null, (_, _) => CopyAccessAddress());
        menu.Items.Add("Copy log path", null, (_, _) => CopyLogPath());
        menu.Items.Add("Change password", null, (_, _) => ChangePassword());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Exit());
        return menu;
    }

    private async Task StartServerAsync()
    {
        try
        {
            await _server.StartAsync(CancellationToken.None);
            AppLogger.Info($"Server started. AccessUrls={string.Join(", ", _server.AccessUrls)}");
            _notifyIcon.ShowBalloonTip(
                4000,
                "Remote Touchpad started",
                $"Open on phone: {GetPrimaryAccessUrl()}\nPassword: {_config.Password}",
                ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Server failed to start.", ex);
            MessageBox.Show(
                $"Failed to start web server: {ex.Message}",
                "Remote Touchpad",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            ExitThread();
        }
    }

    private void ShowAccessAddress()
    {
        MessageBox.Show(
            $"Open this address:\n\n{string.Join("\n", _server.AccessUrls)}\n\nPassword: {_config.Password}\n\nLog file:\n{AppLogger.LogPath}",
            "Remote Touchpad",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void CopyAccessAddress()
    {
        var url = GetPrimaryAccessUrl();
        Clipboard.SetText(url);
        AppLogger.Info($"Access URL copied. Url={url}");
        _notifyIcon.ShowBalloonTip(2000, "Access address copied", url, ToolTipIcon.Info);
    }

    private void CopyLogPath()
    {
        Clipboard.SetText(AppLogger.LogPath);
        AppLogger.Info("Log path copied.");
        _notifyIcon.ShowBalloonTip(2000, "Log path copied", AppLogger.LogPath, ToolTipIcon.Info);
    }

    private void ChangePassword()
    {
        using var form = new PasswordForm(_config.Password);
        if (form.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        _config.Password = form.Password;
        _config.AuthToken = Guid.NewGuid().ToString("N");
        _configStore.Save(_config);
        AppLogger.Info($"Password changed. PasswordLength={_config.Password.Length}, TokenLength={_config.AuthToken.Length}");
        _notifyIcon.ShowBalloonTip(2500, "Password updated", "Logged-in devices need to enter the new password.", ToolTipIcon.Info);
    }

    private string GetPrimaryAccessUrl()
    {
        return _server.AccessUrls.FirstOrDefault(url => !url.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase))
            ?? _server.AccessUrls.First();
    }

    private void Exit()
    {
        AppLogger.Info("Tray app exiting.");
        _notifyIcon.Visible = false;
        ExitThread();
    }
}
