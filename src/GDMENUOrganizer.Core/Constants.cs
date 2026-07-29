using System;
using System.Reflection;

namespace GDMENUOrganizer.Core
{
    public static class Constants
    {
        public const string NameTextFile = "name.txt";
        public const string SerialTextFile = "serial.txt";
        public const string ErrorTextFile = "error.txt";
        public const string JsonGdItemFile = "item.json";
        //private const string InfoTextFile = "info.txt";
        public const string MenuConfigTextFile = "GDEMU.ini";
        public const string GdiShrinkBlacklistFile = "gdishrink_blacklist.txt";
        public const string PS1GameDBFile = "gamedb.json";
        public const string DuckStationGameDbYamlUrl =
            "https://raw.githubusercontent.com/stenzek/duckstation/master/data/resources/gamedb.yaml";
        public static readonly TimeSpan PsGameDbRefreshInterval = TimeSpan.FromDays(7);
        public const string DefaultImageFileName = "disc";
        /// <summary>App version from MSBuild AppVersion / InformationalVersion (set from git tag in CI).</summary>
        public static string Version { get; } =
            typeof(Constants).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
            ?? "dev";
        public static readonly string k_UnknownDiscNumber = "?/?";
        public static readonly string TempFolderName = "GDMENUOrganizer";
    }
}
