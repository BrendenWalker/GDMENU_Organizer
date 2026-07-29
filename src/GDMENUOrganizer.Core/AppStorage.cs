using System;
using System.IO;

namespace GDMENUOrganizer.Core
{
    /// <summary>
    /// OS-agnostic ApplicationData root for settings, SQLite DB, and cached downloads.
    /// </summary>
    public static class AppStorage
    {
        public const string AppFolderName = "GDMENUOrganizer";
        public const string DatabaseFileName = "app.db";
        public const string SettingsFileName = "settings.json";
        public const string CachedGameDbYamlFileName = "gamedb.yaml";

        public static string RootDirectory
        {
            get
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    AppFolderName
                );
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static string DatabasePath => Path.Combine(RootDirectory, DatabaseFileName);

        public static string SettingsPath => Path.Combine(RootDirectory, SettingsFileName);

        public static string CachedGameDbYamlPath =>
            Path.Combine(RootDirectory, CachedGameDbYamlFileName);
    }
}
