#nullable enable
using System.Threading.Tasks;
using GDMENUOrganizer.Core.Database;

namespace GDMENUOrganizer.Core
{
    // Catalog data from DuckStation (https://github.com/stenzek/duckstation) — stored in SQLite.
    public static class PlayStationDB
    {
        public static async Task<PSDBEntry?> FindBySerialAsync(string? serial)
        {
            await AppDatabase.EnsureCreatedAsync().ConfigureAwait(false);
            return await AppDatabase.Instance.PsGames
                .FindBySerialAsync(serial)
                .ConfigureAwait(false);
        }
    }

    public class PSDBEntry
    {
        public string serial { get; set; } = string.Empty;
        public string? name { get; set; }
        public string? releaseDate { get; set; }
    }
}
