using System.Text.Json;

namespace PApps;

public static class SettingsStore
{
    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "launcher.settings.json");
    public static LauncherSettings Load()
    {
        var text = File.ReadAllText(FilePath);
        var settings = JsonSerializer.Deserialize<LauncherSettings>(text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new JsonException("Settings must contain an object.");
        settings.Apps ??= [];
        // Existing installations have already completed Setup before this marker was introduced.
        using var json = JsonDocument.Parse(text);
        if (!json.RootElement.EnumerateObject().Any(p => p.Name.Equals("SetupCompletedFor", StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrWhiteSpace(settings.Repository))
            settings.SetupCompletedFor = Path.GetFullPath(settings.Repository);
        return settings;
    }
    public static bool NeedsSetup(LauncherSettings settings) => string.IsNullOrWhiteSpace(settings.Repository)
        || string.IsNullOrWhiteSpace(settings.SetupCompletedFor)
        || !string.Equals(Path.GetFullPath(settings.Repository).TrimEnd('\\'), Path.GetFullPath(settings.SetupCompletedFor).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
    public static void Save(LauncherSettings settings)
    {
        File.WriteAllText(FilePath + ".tmp", JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(FilePath + ".tmp", FilePath, true);
    }
    public static string AppPath(LauncherSettings settings, AppEntry app)
    {
        if (Path.IsPathRooted(app.Executable)) return Path.GetFullPath(app.Executable);
        var root = Path.GetFullPath(settings.Repository).TrimEnd('\\') + "\\";
        var result = Path.GetFullPath(Path.Combine(root, app.Executable));
        if (!result.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("The app points outside the main folder.");
        return result;
    }
    public static string DisplayName(LauncherSettings settings, AppEntry app)
    {
        if (!settings.HidePortable) return app.Name;
        var result = System.Text.RegularExpressions.Regex.Replace(app.Name, "Portable", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim(' ', '-', '_');
        return string.IsNullOrWhiteSpace(result) ? app.Name : result;
    }
}
