using System.Text.Json;

namespace AsusFanProfileSwitcher.Services;

internal sealed class AppSettings
{
    public Dictionary<string, string> ProfileDisplayNames { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> FanAliases { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class SettingsStore
{
    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "AsusFanProfileSwitcher",
        "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new AppSettings();
            }

            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsPath))
                ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_settingsPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = _settingsPath + ".tmp";
        File.WriteAllText(
            temporaryPath,
            JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporaryPath, _settingsPath, true);
    }

    public static string GetProfileDisplayName(AppSettings settings, string fileName, string fallback)
    {
        var match = settings.ProfileDisplayNames.FirstOrDefault(
            entry => string.Equals(entry.Key, fileName, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(match.Value) ? fallback : match.Value;
    }

    public static string GetFanAlias(AppSettings settings, string sensorId, string fallback)
    {
        var match = settings.FanAliases.FirstOrDefault(
            entry => string.Equals(entry.Key, sensorId, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(match.Value) ? fallback : match.Value;
    }
}
