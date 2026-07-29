#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace GDMENUOrganizer.Core.Database
{
    public sealed class LibraryGameRecord
    {
        public long Id { get; set; }
        public string SourcePath { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? ProductNumber { get; set; }
        public string? Disc { get; set; }
        public long LengthBytes { get; set; }
        public string? FileFormat { get; set; }
        public int SpecialDisc { get; set; }
        public string? ReleaseDate { get; set; }
        public string? Region { get; set; }
        public string SyncStatus { get; set; } = LibrarySyncStatuses.Present;
    }

    public static class LibrarySyncStatuses
    {
        public const string Present = "present";
        public const string New = "new";
        public const string Missing = "missing";

        public static string Normalize(string? value)
        {
            if (string.Equals(value, New, StringComparison.OrdinalIgnoreCase))
                return New;
            if (string.Equals(value, Missing, StringComparison.OrdinalIgnoreCase))
                return Missing;
            return Present;
        }
    }

    public sealed class LibraryRepository
    {
        private const string SelectColumns =
            "Id, SourcePath, Name, ProductNumber, Disc, LengthBytes, FileFormat, SpecialDisc, ReleaseDate, Region, SyncStatus";

        private readonly AppDatabase _db;

        public LibraryRepository(AppDatabase db)
        {
            _db = db;
        }

        public async Task<LibraryGameRecord?> GetByIdAsync(
            long id,
            CancellationToken cancellationToken = default
        )
        {
            await using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText =
                $@"
SELECT {SelectColumns}
FROM LibraryGames WHERE Id = $id LIMIT 1;";
            cmd.Parameters.AddWithValue("$id", id);
            return await ReadSingleAsync(cmd, cancellationToken).ConfigureAwait(false);
        }

        public async Task<LibraryGameRecord?> GetByPathAsync(
            string sourcePath,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                return null;

            await using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText =
                $@"
SELECT {SelectColumns}
FROM LibraryGames WHERE SourcePath = $path LIMIT 1;";
            cmd.Parameters.AddWithValue("$path", sourcePath);
            return await ReadSingleAsync(cmd, cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<LibraryGameRecord>> ListAsync(
            CancellationToken cancellationToken = default
        )
        {
            var list = new List<LibraryGameRecord>();
            await using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText =
                $@"
SELECT {SelectColumns}
FROM LibraryGames
ORDER BY Name COLLATE NOCASE, SourcePath;";

            await using var reader = await cmd
                .ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                list.Add(ReadRecord(reader));

            return list;
        }

        public async Task<long> UpsertAsync(
            LibraryGameRecord record,
            CancellationToken cancellationToken = default
        )
        {
            await using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText =
                @"
INSERT INTO LibraryGames (SourcePath, Name, ProductNumber, Disc, LengthBytes, FileFormat, SpecialDisc, ReleaseDate, Region, SyncStatus)
VALUES ($sourcePath, $name, $productNumber, $disc, $lengthBytes, $fileFormat, $specialDisc, $releaseDate, $region, $syncStatus)
ON CONFLICT(SourcePath) DO UPDATE SET
    Name = excluded.Name,
    ProductNumber = excluded.ProductNumber,
    Disc = excluded.Disc,
    LengthBytes = excluded.LengthBytes,
    FileFormat = excluded.FileFormat,
    SpecialDisc = excluded.SpecialDisc,
    ReleaseDate = excluded.ReleaseDate,
    Region = excluded.Region,
    SyncStatus = excluded.SyncStatus
RETURNING Id;";
            cmd.Parameters.AddWithValue("$sourcePath", record.SourcePath);
            cmd.Parameters.AddWithValue("$name", (object?)record.Name ?? DBNull.Value);
            cmd.Parameters.AddWithValue(
                "$productNumber",
                (object?)record.ProductNumber ?? DBNull.Value
            );
            cmd.Parameters.AddWithValue("$disc", (object?)record.Disc ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$lengthBytes", record.LengthBytes);
            cmd.Parameters.AddWithValue("$fileFormat", (object?)record.FileFormat ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$specialDisc", record.SpecialDisc);
            cmd.Parameters.AddWithValue(
                "$releaseDate",
                (object?)record.ReleaseDate ?? DBNull.Value
            );
            cmd.Parameters.AddWithValue("$region", (object?)record.Region ?? DBNull.Value);
            cmd.Parameters.AddWithValue(
                "$syncStatus",
                LibrarySyncStatuses.Normalize(record.SyncStatus)
            );

            var id = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt64(id);
        }

        public async Task SetSyncStatusAsync(
            long id,
            string syncStatus,
            CancellationToken cancellationToken = default
        )
        {
            await using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = "UPDATE LibraryGames SET SyncStatus = $status WHERE Id = $id;";
            cmd.Parameters.AddWithValue("$status", LibrarySyncStatuses.Normalize(syncStatus));
            cmd.Parameters.AddWithValue("$id", id);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
        {
            await using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = "DELETE FROM LibraryGames WHERE Id = $id;";
            cmd.Parameters.AddWithValue("$id", id);
            return await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
        }

        private static async Task<LibraryGameRecord?> ReadSingleAsync(
            SqliteCommand cmd,
            CancellationToken cancellationToken
        )
        {
            await using var reader = await cmd
                .ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                return null;
            return ReadRecord(reader);
        }

        private static LibraryGameRecord ReadRecord(SqliteDataReader reader)
        {
            return new LibraryGameRecord
            {
                Id = reader.GetInt64(0),
                SourcePath = reader.GetString(1),
                Name = reader.IsDBNull(2) ? null : reader.GetString(2),
                ProductNumber = reader.IsDBNull(3) ? null : reader.GetString(3),
                Disc = reader.IsDBNull(4) ? null : reader.GetString(4),
                LengthBytes = reader.GetInt64(5),
                FileFormat = reader.IsDBNull(6) ? null : reader.GetString(6),
                SpecialDisc = reader.GetInt32(7),
                ReleaseDate = reader.IsDBNull(8) ? null : reader.GetString(8),
                Region = reader.IsDBNull(9) ? null : reader.GetString(9),
                SyncStatus = LibrarySyncStatuses.Normalize(
                    reader.IsDBNull(10) ? null : reader.GetString(10)
                )
            };
        }
    }
}
