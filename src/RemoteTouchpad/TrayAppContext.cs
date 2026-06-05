using RemoteTouchpad.Web;
using Microsoft.Win32;
using System.Diagnostics;

namespace RemoteTouchpad;

public sealed class TrayAppContext : ApplicationContext
{
    private const string StartupRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupValueName = "RemoteTouchpad";

    private readonly ConfigStore _configStore = new();
    private readonly AppConfig _config;
    private readonly NotifyIcon _notifyIcon;
    private readonly RemoteControlServer _server;
    private readonly Icon _appIcon;
    private ToolStripMenuItem? _startupMenuItem;

    public TrayAppContext()
    {
        AppLogger.Info($"Tray app starting. BaseDirectory={AppContext.BaseDirectory}");

        _config = _configStore.Load();
        AppLogger.Info($"Config loaded. Port={_config.Port}, PasswordLength={_config.Password.Length}, SessionTokenLength={_config.SessionToken.Length}, LogPath={AppLogger.LogPath}");

        _server = new RemoteControlServer(_config, _configStore);
        _appIcon = LoadAppIcon();

        _notifyIcon = new NotifyIcon
        {
            Icon = _appIcon,
            Text = "\u8fdc\u7a0b\u89e6\u63a7\u677f",
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
            _appIcon.Dispose();
            _server.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        base.Dispose(disposing);
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("\u663e\u793a\u8bbf\u95ee\u5730\u5740", null, (_, _) => ShowAccessAddress());
        menu.Items.Add("\u590d\u5236\u8bbf\u95ee\u5730\u5740", null, (_, _) => CopyAccessAddress());
        menu.Items.Add("\u6253\u5f00\u4e8c\u7ef4\u7801\u9875\u9762", null, (_, _) => OpenQrPage());
        menu.Items.Add("\u590d\u5236\u65e5\u5fd7\u8def\u5f84", null, (_, _) => CopyLogPath());
        menu.Items.Add("\u4fee\u6539\u5bc6\u7801", null, (_, _) => ChangePassword());
        menu.Items.Add("\u914d\u7f6e\u5a92\u4f53\u952e\u4f4d", null, (_, _) => ConfigureMediaBindings());
        _startupMenuItem = new ToolStripMenuItem("\u5f00\u673a\u81ea\u542f")
        {
            CheckOnClick = true,
            Checked = IsStartupEnabled()
        };
        _startupMenuItem.CheckedChanged += StartupMenuItemCheckedChanged;
        menu.Items.Add(_startupMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("\u9000\u51fa", null, (_, _) => Exit());
        menu.Opening += (_, _) =>
        {
            if (_startupMenuItem is not null)
            {
                _startupMenuItem.CheckedChanged -= StartupMenuItemCheckedChanged;
                _startupMenuItem.Checked = IsStartupEnabled();
                _startupMenuItem.CheckedChanged += StartupMenuItemCheckedChanged;
            }
        };
        return menu;
    }

    private void StartupMenuItemCheckedChanged(object? sender, EventArgs eventArgs)
    {
        if (_startupMenuItem is not null)
        {
            SetStartupEnabled(_startupMenuItem.Checked);
        }
    }

    private async Task StartServerAsync()
    {
        try
        {
            await _server.StartAsync(CancellationToken.None);
            AppLogger.Info($"Server started. AccessUrls={string.Join(", ", _server.AccessUrls)}");
            _notifyIcon.ShowBalloonTip(
                4000,
                "\u8fdc\u7a0b\u89e6\u63a7\u677f\u5df2\u542f\u52a8",
                $"\u624b\u673a\u8bbf\u95ee\uff1a{GetPrimaryAccessUrl()}\n\u5bc6\u7801\uff1a{_config.Password}",
                ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Server failed to start.", ex);
            MessageBox.Show(
                $"\u542f\u52a8 Web \u670d\u52a1\u5931\u8d25\uff1a{ex.Message}",
                "\u8fdc\u7a0b\u89e6\u63a7\u677f",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            ExitThread();
        }
    }

    private void ShowAccessAddress()
    {
        MessageBox.Show(
            $"\u8bf7\u5728\u540c\u4e00\u5c40\u57df\u7f51\u8bbe\u5907\u4e0a\u6253\u5f00\uff1a\n\n{string.Join("\n", _server.AccessUrls)}\n\n\u5f53\u524d\u5bc6\u7801\uff1a{_config.Password}\n\n\u65e5\u5fd7\u6587\u4ef6\uff1a\n{AppLogger.LogPath}",
            "\u8fdc\u7a0b\u89e6\u63a7\u677f",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void CopyAccessAddress()
    {
        var url = GetPrimaryAccessUrl();
        Clipboard.SetText(url);
        AppLogger.Info($"Access URL copied. Url={url}");
        _notifyIcon.ShowBalloonTip(2000, "\u8bbf\u95ee\u5730\u5740\u5df2\u590d\u5236", url, ToolTipIcon.Info);
    }

    private void CopyLogPath()
    {
        Clipboard.SetText(AppLogger.LogPath);
        AppLogger.Info("Log path copied.");
        _notifyIcon.ShowBalloonTip(2000, "\u65e5\u5fd7\u8def\u5f84\u5df2\u590d\u5236", AppLogger.LogPath, ToolTipIcon.Info);
    }

    private void OpenQrPage()
    {
        var url = _server.LocalQrPageUrl;
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        AppLogger.Info($"QR page opened. Url={url}");
    }

    private void ChangePassword()
    {
        using var form = new PasswordForm(_config.Password);
        if (form.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        _config.Password = form.Password;
        _config.SessionToken = Guid.NewGuid().ToString("N");
        _configStore.Save(_config);
        AppLogger.Info($"Password changed. PasswordLength={_config.Password.Length}, SessionTokenLength={_config.SessionToken.Length}");
        _notifyIcon.ShowBalloonTip(2500, "\u5bc6\u7801\u5df2\u66f4\u65b0", "\u5df2\u767b\u5f55\u8bbe\u5907\u9700\u8981\u91cd\u65b0\u8f93\u5165\u5bc6\u7801\u3002", ToolTipIcon.Info);
    }

    private void ConfigureMediaBindings()
    {
        using var form = new MediaBindingsForm(_config.MediaBindings);
        if (form.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        _config.MediaBindings = form.Bindings;
        _configStore.Save(_config);
        AppLogger.Info($"Media bindings changed from tray. PlayPause={_config.MediaBindings.PlayPause}, Previous={_config.MediaBindings.Previous}, Next={_config.MediaBindings.Next}, VolumeDown={_config.MediaBindings.VolumeDown}, VolumeUp={_config.MediaBindings.VolumeUp}");
        _notifyIcon.ShowBalloonTip(2500, "\u5a92\u4f53\u952e\u4f4d\u5df2\u4fdd\u5b58", "\u624b\u673a\u63a7\u5236\u9875\u4f1a\u4f7f\u7528\u65b0\u952e\u4f4d\u3002", ToolTipIcon.Info);
    }

    private string GetPrimaryAccessUrl()
    {
        return _server.PrimaryAccessUrl;
    }

    private static Icon LoadAppIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        return File.Exists(iconPath) ? new Icon(iconPath) : (Icon)SystemIcons.Application.Clone();
    }

    private static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryPath, writable: false);
        var value = key?.GetValue(StartupValueName)?.ToString();
        return string.Equals(NormalizeStartupCommand(value), NormalizeStartupCommand(GetStartupCommand()), StringComparison.OrdinalIgnoreCase);
    }

    private void SetStartupEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(StartupRegistryPath);
            if (enabled)
            {
                key.SetValue(StartupValueName, GetStartupCommand(), RegistryValueKind.String);
                AppLogger.Info("Startup enabled.");
                _notifyIcon.ShowBalloonTip(2000, "\u5f00\u673a\u81ea\u542f\u5df2\u5f00\u542f", "\u4e0b\u6b21\u767b\u5f55 Windows \u65f6\u4f1a\u81ea\u52a8\u542f\u52a8\u3002", ToolTipIcon.Info);
            }
            else
            {
                key.DeleteValue(StartupValueName, throwOnMissingValue: false);
                AppLogger.Info("Startup disabled.");
                _notifyIcon.ShowBalloonTip(2000, "\u5f00\u673a\u81ea\u542f\u5df2\u5173\u95ed", "\u4e0d\u4f1a\u518d\u968f Windows \u767b\u5f55\u81ea\u52a8\u542f\u52a8\u3002", ToolTipIcon.Info);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to update startup setting.", ex);
            MessageBox.Show(
                $"\u66f4\u65b0\u5f00\u673a\u81ea\u542f\u5931\u8d25\uff1a{ex.Message}",
                "\u8fdc\u7a0b\u89e6\u63a7\u677f",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static string GetStartupCommand()
    {
        var executablePath = Environment.ProcessPath ?? Application.ExecutablePath;
        return $"\"{executablePath}\"";
    }

    private static string? NormalizeStartupCommand(string? command)
    {
        return string.IsNullOrWhiteSpace(command)
            ? null
            : command.Trim().Trim('"');
    }

    private void Exit()
    {
        AppLogger.Info("Tray app exiting.");
        _notifyIcon.Visible = false;
        ExitThread();
    }
}
