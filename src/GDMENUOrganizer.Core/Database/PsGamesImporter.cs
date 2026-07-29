#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using YamlDotNet.RepresentationModel;

namespace GDMENUOrganizer.Core.Database
{
    public static class PsGamesImporter
    {
        public const string MetaImported = "PsGamesImported";
        public const string MetaEtag = "PsGamesEtag";
        public const string MetaLastChecked = "PsGamesLastCheckedUtc";

        public static async Task EnsureCatalogAsync(
            AppDatabase db,
            bool forceRefresh,
            CancellationToken cancellationToken = default
        )
        {
            var count = await db.PsGames.CountAsync(cancellationToken).ConfigureAwait(false);
            var lastCheckedText = await db.GetMetaAsync(MetaLastChecked).ConfigureAwait(false);
            var isStale =
                forceRefresh
                || count == 0
                || !DateTime.TryParse(
                    lastCheckedText,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var lastChecked
                )
                || DateTime.UtcNow - lastChecked >= Constants.PsGameDbRefreshInterval;

            if (!isStale)
                return;

            var existingEtag = await db.GetMetaAsync(MetaEtag).ConfigureAwait(false);
            var download = await PsGamesDownloader
                .TryDownloadAsync(existingEtag, cancellationToken)
                .ConfigureAwait(false);

            if (download.Status == PsGamesDownloadStatus.NotModified)
            {
                await db.SetMetaAsync(MetaLastChecked, DateTime.UtcNow.ToString("O"))
                    .ConfigureAwait(false);
                return;
            }

            if (
                download.Status == PsGamesDownloadStatus.Downloaded
                && !string.IsNullOrEmpty(download.LocalYamlPath)
            )
            {
                var entries = ParseYamlFile(download.LocalYamlPath);
                if (entries.Count > 0)
                {
                    await ReplaceCatalogAsync(db, entries, cancellationToken).ConfigureAwait(false);
                    await db.SetMetaAsync(MetaEtag, download.ETag).ConfigureAwait(false);
                    await db.SetMetaAsync(MetaImported, "1").ConfigureAwait(false);
                    await db.SetMetaAsync(MetaLastChecked, DateTime.UtcNow.ToString("O"))
                        .ConfigureAwait(false);
                    return;
                }
            }

            // Offline / failed download: keep existing rows if any, else seed from bundled JSON.
            if (count > 0)
            {
                await db.SetMetaAsync(MetaLastChecked, DateTime.UtcNow.ToString("O"))
                    .ConfigureAwait(false);
                return;
            }

            var seedPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                Constants.PS1GameDBFile
            );
            if (!File.Exists(seedPath))
                return;

            var seedEntries = ParseJsonFile(seedPath);
            if (seedEntries.Count == 0)
                return;

            await ReplaceCatalogAsync(db, seedEntries, cancellationToken).ConfigureAwait(false);
            await db.SetMetaAsync(MetaImported, "1").ConfigureAwait(false);
            await db.SetMetaAsync(MetaLastChecked, DateTime.UtcNow.ToString("O"))
                .ConfigureAwait(false);
        }

        private static async Task ReplaceCatalogAsync(
            AppDatabase db,
            IReadOnlyList<PSDBEntry> entries,
            CancellationToken cancellationToken
        )
        {
            await using var tx = await db.Connection
                .BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);

            await using (var delete = db.Connection.CreateCommand())
            {
                delete.Transaction = (SqliteTransaction)tx;
                delete.CommandText = "DELETE FROM PsGames;";
                await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using (var insert = db.Connection.CreateCommand())
            {
                insert.Transaction = (SqliteTransaction)tx;
                insert.CommandText =
                    @"
INSERT OR REPLACE INTO PsGames (Serial, Name, ReleaseDate)
VALUES ($serial, $name, $releaseDate);";
                var serial = insert.Parameters.Add("$serial", SqliteType.Text);
                var name = insert.Parameters.Add("$name", SqliteType.Text);
                var releaseDate = insert.Parameters.Add("$releaseDate", SqliteType.Text);

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (string.IsNullOrWhiteSpace(entry.serial))
                        continue;
                    var serialValue = entry.serial.Trim();
                    if (!seen.Add(serialValue))
                        continue;

                    serial.Value = serialValue;
                    name.Value = (object?)entry.name ?? DBNull.Value;
                    releaseDate.Value = (object?)entry.releaseDate ?? DBNull.Value;
                    await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        public static List<PSDBEntry> ParseYamlFile(string path)
        {
            using var reader = File.OpenText(path);
            var yaml = new YamlStream();
            yaml.Load(reader);

            var entries = new List<PSDBEntry>();
            if (yaml.Documents.Count == 0)
                return entries;

            if (yaml.Documents[0].RootNode is not YamlMappingNode root)
                return entries;

            foreach (var (keyNode, valueNode) in root.Children)
            {
                var serial = keyNode.ToString();
                if (string.IsNullOrWhiteSpace(serial) || valueNode is not YamlMappingNode game)
                    continue;

                string? name = null;
                string? releaseDate = null;

                if (
                    game.Children.TryGetValue(new YamlScalarNode("name"), out var nameNode)
                )
                    name = nameNode.ToString();

                if (
                    game.Children.TryGetValue(new YamlScalarNode("metadata"), out var metaNode)
                    && metaNode is YamlMappingNode metadata
                    && metadata.Children.TryGetValue(
                        new YamlScalarNode("releaseDate"),
                        out var dateNode
                    )
                )
                    releaseDate = dateNode.ToString();

                entries.Add(
                    new PSDBEntry
                    {
                        serial = serial,
                        name = name,
                        releaseDate = releaseDate
                    }
                );
            }

            return entries;
        }

        public static List<PSDBEntry> ParseJsonFile(string path)
        {
            using var stream = File.OpenRead(path);
            var list = JsonSerializer.Deserialize<List<PSDBEntry>>(stream);
            return list?.Where(x => !string.IsNullOrWhiteSpace(x.serial)).ToList()
                ?? new List<PSDBEntry>();
        }
    }
}
