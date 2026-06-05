using System.Windows.Forms;

namespace RemoteTouchpad;

public sealed class AppConfig
{
    public int Port { get; set; } = 8765;
    public string Password { get; set; } = "123456";
    public double Sensitivity { get; set; } = 1.0;
    public double MagnifierZoom { get; set; } = 2.0;
    public int MagnifierSize { get; set; } = 260;
    public MediaBindings MediaBindings { get; set; } = new();
    public string SessionToken { get; set; } = Guid.NewGuid().ToString("N");
}

public sealed class MediaBindings
{
    public string PlayPause { get; set; } = MediaBinding.SystemPlayPause;
    public string Previous { get; set; } = MediaBinding.SystemPrevious;
    public string Next { get; set; } = MediaBinding.SystemNext;
    public string VolumeDown { get; set; } = MediaBinding.SystemVolumeDown;
    public string VolumeUp { get; set; } = MediaBinding.SystemVolumeUp;
}

public static class MediaBinding
{
    public const string SystemPlayPause = "systemPlayPause";
    public const string SystemPrevious = "systemPrevious";
    public const string SystemNext = "systemNext";
    public const string SystemVolumeDown = "systemVolumeDown";
    public const string SystemVolumeUp = "systemVolumeUp";

    private static readonly HashSet<string> Allowed =
    [
        SystemPlayPause,
        SystemPrevious,
        SystemNext,
        SystemVolumeDown,
        SystemVolumeUp
    ];

    public static bool IsAllowed(string? binding)
    {
        return !string.IsNullOrWhiteSpace(binding)
            && (Allowed.Contains(binding) || binding.StartsWith("key:", StringComparison.OrdinalIgnoreCase));
    }

    public static string Normalize(string? binding, string fallback)
    {
        return IsAllowed(binding) ? binding! : fallback;
    }

    public static string FromKeys(Keys key, bool control, bool shift, bool alt)
    {
        var modifiers = new List<string>();
        if (control)
        {
            modifiers.Add("Control");
        }

        if (shift)
        {
            modifiers.Add("Shift");
        }

        if (alt)
        {
            modifiers.Add("Alt");
        }

        return $"key:{string.Join("+", modifiers)}:{key}";
    }

    public static string GetDisplayName(string binding)
    {
        return binding switch
        {
            SystemPlayPause => "\u7cfb\u7edf\u64ad\u653e/\u6682\u505c",
            SystemPrevious => "\u7cfb\u7edf\u4e0a\u4e00\u66f2",
            SystemNext => "\u7cfb\u7edf\u4e0b\u4e00\u66f2",
            SystemVolumeDown => "\u7cfb\u7edf\u97f3\u91cf\u51cf",
            SystemVolumeUp => "\u7cfb\u7edf\u97f3\u91cf\u52a0",
            _ when TryParseKeyBinding(binding, out var parsed) => parsed.DisplayName,
            _ => binding
        };
    }

    public static bool TryParseKeyBinding(string binding, out ParsedKeyBinding parsed)
    {
        parsed = default;
        if (!binding.StartsWith("key:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parts = binding.Split(':', 3);
        if (parts.Length != 3 || !Enum.TryParse(parts[2], ignoreCase: true, out Keys key))
        {
            return false;
        }

        var modifiers = parts[1]
            .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        parsed = new ParsedKeyBinding(
            key,
            modifiers.Contains("Control"),
            modifiers.Contains("Shift"),
            modifiers.Contains("Alt"));
        return true;
    }
}

public readonly record struct ParsedKeyBinding(Keys Key, bool Control, bool Shift, bool Alt)
{
    public string DisplayName
    {
        get
        {
            var parts = new List<string>();
            if (Control)
            {
                parts.Add("Ctrl");
            }

            if (Shift)
            {
                parts.Add("Shift");
            }

            if (Alt)
            {
                parts.Add("Alt");
            }

            parts.Add(Key.ToString());
            return string.Join(" + ", parts);
        }
    }
}
