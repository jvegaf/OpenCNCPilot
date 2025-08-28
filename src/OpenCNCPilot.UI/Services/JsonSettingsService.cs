using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace OpenCNCPilot.UI.Services;

public class JsonSettingsService : ISettingsService
{
    private readonly string settingsPath;

    public JsonSettingsService() : this(GetConfigDirectory()) { }

    public JsonSettingsService(string baseDirectory)
    {
        Directory.CreateDirectory(baseDirectory);
        settingsPath = Path.Combine(baseDirectory, "appsettings.json");
    }

    public async Task<AppSettings> LoadAsync()
    {
        try
        {
            if (!File.Exists(settingsPath))
                return new AppSettings();

            await using var fs = File.OpenRead(settingsPath);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(fs) ?? new AppSettings();
            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings)
    {
        var opts = new JsonSerializerOptions { WriteIndented = true };
        await using var fs = File.Create(settingsPath);
        await JsonSerializer.SerializeAsync(fs, settings, opts);
    }

    private static string GetConfigDirectory()
    {
        // Cross-platform per-user config directory
        var appName = "OpenCNCPilot";
        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, appName);
        }
        if (OperatingSystem.IsMacOS())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            return Path.Combine(home, ".config", appName);
        }
        // Linux and others
        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var dir = !string.IsNullOrWhiteSpace(xdg) ? xdg : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".config");
        return Path.Combine(dir, appName);
    }
}
