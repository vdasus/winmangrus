using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace Winmangrus;

enum WindowState { Normal, Maximized, Minimized }

sealed class SavedWindow
{
    public string Exe { get; set; } = "";
    public string Class { get; set; } = "";
    public string Title { get; set; } = "";
    public int Left { get; set; }
    public int Top { get; set; }
    public int Right { get; set; }
    public int Bottom { get; set; }
    public WindowState State { get; set; }
}

sealed class Profile
{
    public string Name { get; set; } = "";
    /// <summary>Monitor layout at save time; auto-restore picks the profile that matches the current one.</summary>
    public string Monitors { get; set; } = "";
    public List<SavedWindow> Windows { get; set; } = [];
}

sealed class Settings
{
    public bool AutoRestore { get; set; } = true;
    public List<Profile> Profiles { get; set; } = [];

    // Relaxed escaping keeps non-ASCII window titles readable in the file.
    static readonly SettingsJson Json = new(new JsonSerializerOptions(SettingsJson.Default.Options) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    static string FilePath => Path.Combine(AppContext.BaseDirectory, "winmangrus.json");

    public Profile? Find(string name) => Profiles.Find(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public static Settings Load()
    {
        if (!File.Exists(FilePath)) return new();
        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(FilePath), Json.Settings) ?? new();
        }
        catch (JsonException)
        {
            // Keep the broken file for manual recovery instead of silently overwriting it on the next save.
            File.Copy(FilePath, FilePath + ".bad", true);
            return new();
        }
    }

    public void Save()
    {
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, Json.Settings));
        File.Move(tmp, FilePath, true);
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(Settings))]
partial class SettingsJson : JsonSerializerContext;

static class Startup
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run", Name = "winmangrus";
    static string Command => $"\"{Environment.ProcessPath}\"";

    public static bool Enabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(Name) as string == Command;
        }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value) key.SetValue(Name, Command);
            else key.DeleteValue(Name, false);
        }
    }
}
