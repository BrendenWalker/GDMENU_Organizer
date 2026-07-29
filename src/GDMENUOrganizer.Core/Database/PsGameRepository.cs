#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace GDMENUOrganizer.Core.Database
{
    public sealed class PsGameRepository
    {
        private readonly AppDatabase _db;

        public PsGameRepository(AppDatabase db)
        {
            _db = db;
        }

        public async Task<long> CountAsync(CancellationToken cancellationToken = default)
        {
            await using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM PsGames;";
            var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt64(result);
        }

        public async Task<PSDBEntry?> FindBySerialAsync(
            string? serial,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrWhiteSpace(serial))
                return null;

            await using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText =
                @"
SELECT Serial, Name, ReleaseDate
FROM PsGames
WHERE Serial = $serial COLLATE NOCASE
LIMIT 1;";
            cmd.Parameters.AddWithValue("$serial", serial.Trim());

            await using var reader = await cmd
                .ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                return null;

            return new PSDBEntry
            {
                serial = reader.GetString(0),
                name = reader.IsDBNull(1) ? null : reader.GetString(1),
                releaseDate = reader.IsDBNull(2) ? null : reader.GetString(2)
            };
        }

        public async Task<IReadOnlyList<PSDBEntry>> SearchByNameAsync(
            string query,
            int limit = 50,
            CancellationToken cancellationToken = default
        )
        {
            var results = new List<PSDBEntry>();
            if (string.IsNullOrWhiteSpace(query))
                return results;

            await using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText =
                @"
SELECT Serial, Name, ReleaseDate
FROM PsGames
WHERE Name LIKE $query
ORDER BY Name
LIMIT $limit;";
            cmd.Parameters.AddWithValue("$query", "%" + query.Trim() + "%");
            cmd.Parameters.AddWithValue("$limit", limit);

            await using var reader = await cmd
                .ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(
                    new PSDBEntry
                    {
                        serial = reader.GetString(0),
                        name = reader.IsDBNull(1) ? null : reader.GetString(1),
                        releaseDate = reader.IsDBNull(2) ? null : reader.GetString(2)
                    }
                );
            }

            return results;
        }
    }
}
