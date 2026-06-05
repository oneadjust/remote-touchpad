using System.Text.Json;

namespace RemoteTouchpad;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public ConfigStore()
    {
        Directory.CreateDirectory(ConfigDirectory);
    }

    public string ConfigDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RemoteTouchpad");

    private string ConfigPath => Path.Combine(ConfigDirectory, "config.json");

    public AppConfig Load()
    {
        if (!File.Exists(ConfigPath))
        {
            var created = new AppConfig();
            Save(created);
            return created;
        }

        try
        {
            var json = File.ReadAllText(ConfigPath);
            var config = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();

            if (string.IsNullOrWhiteSpace(config.Password))
            {
                config.Password = "123456";
            }

            if (string.IsNullOrWhiteSpace(config.SessionToken))
            {
                config.SessionToken = Guid.NewGuid().ToString("N");
            }

            if (config.Port is < 1024 or > 65535)
            {
                config.Port = 8765;
            }

            if (config.Sensitivity <= 0)
            {
                config.Sensitivity = 1.0;
            }

            config.MagnifierZoom = Math.Clamp(config.MagnifierZoom <= 0 ? 2.0 : config.MagnifierZoom, 1.25, 4.0);
            config.MagnifierSize = Math.Clamp(config.MagnifierSize <= 0 ? 260 : config.MagnifierSize, 160, 420);

            config.MediaBindings ??= new MediaBindings();
            config.MediaBindings.PlayPause = MediaBinding.Normalize(config.MediaBindings.PlayPause, MediaBinding.SystemPlayPause);
            config.MediaBindings.Previous = MediaBinding.Normalize(config.MediaBindings.Previous, MediaBinding.SystemPrevious);
            config.MediaBindings.Next = MediaBinding.Normalize(config.MediaBindings.Next, MediaBinding.SystemNext);
            config.MediaBindings.VolumeDown = MediaBinding.Normalize(config.MediaBindings.VolumeDown, MediaBinding.SystemVolumeDown);
            config.MediaBindings.VolumeUp = MediaBinding.Normalize(config.MediaBindings.VolumeUp, MediaBinding.SystemVolumeUp);

            Save(config);
            return config;
        }
        catch
        {
            var fallback = new AppConfig();
            Save(fallback);
            return fallback;
        }
    }

    public void Save(AppConfig config)
    {
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, JsonOptions));
    }
}
