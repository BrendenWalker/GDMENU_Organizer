using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using GDMENUOrganizer.Core;

namespace GDMENUOrganizer.AvaloniaUI
{
    /// <summary>
    /// Persists Settings-tab user preferences to an OS-agnostic JSON file under ApplicationData.
    /// </summary>
    public class UserSettings
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        public string LibraryPath { get; set; }

        public string TempFolder { get; set; }

        public MenuKind MenuKind { get; set; } = MenuKind.None;

        public static string GetSettingsPath() => AppStorage.SettingsPath;

        public static UserSettings Load()
        {
            try
            {
                var path = GetSettingsPath();
                if (!File.Exists(path))
                    return new UserSettings();

                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<UserSettings>(json, JsonOptions)
                    ?? new UserSettings();
            }
            catch
            {
                return new UserSettings();
            }
        }

        public void Save()
        {
            var path = GetSettingsPath();
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
        }
    }
}
