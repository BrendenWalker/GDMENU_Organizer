#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace GDMENUOrganizer.Core.Database
{
    public sealed class CardRecord
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public sealed class CardGameLink
    {
        public long LibraryGameId { get; set; }
        public int SortOrder { get; set; }
    }

    public sealed class CardRepository
    {
        private readonly AppDatabase _db;

        public CardRepository(AppDatabase db)
        {
            _db = db;
        }

        public async Task<CardRecord?> GetByIdAsync(
            long id,
            CancellationToken cancellationToken = default
        )
        {
            await using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText =
                "SELECT Id, Name, Description FROM Cards WHERE Id = $id LIMIT 1;";
            cmd.Parameters.AddWithValue("$id", id);

            await using var reader = await cmd
                .ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                return null;

            return new CardRecord
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                Description = reader.IsDBNull(2) ? null : reader.GetString(2)
            };
        }

        public async Task<IReadOnlyList<CardRecord>> ListAsync(
            CancellationToken cancellationToken = default
        )
        {
            var list = new List<CardRecord>();
            await using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText =
                "SELECT Id, Name, Description FROM Cards ORDER BY Name COLLATE NOCASE;";

            await using var reader = await cmd
                .ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                list.Add(
                    new CardRecord
                    {
                        Id = reader.GetInt64(0),
                        Name = reader.GetString(1),
                        Description = reader.IsDBNull(2) ? null : reader.GetString(2)
                    }
                );
            }

            return list;
        }

        public async Task<long> CreateAsync(
            string name,
            string? description = null,
            CancellationToken cancellationToken = default
        )
        {
            await using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText =
                @"
INSERT INTO Cards (Name, Description)
VALUES ($name, $description)
RETURNING Id;";
            cmd.Parameters.AddWithValue("$name", name);
            cmd.Parameters.AddWithValue("$description", (object?)description ?? DBNull.Value);
            var id = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt64(id);
        }

        public async Task<bool> UpdateAsync(
            CardRecord card,
            CancellationToken cancellationToken = default
        )
        {
            await using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText =
                @"
UPDATE Cards
SET Name = $name, Description = $description
WHERE Id = $id;";
            cmd.Parameters.AddWithValue("$id", card.Id);
            cmd.Parameters.AddWithValue("$name", card.Name);
            cmd.Parameters.AddWithValue(
                "$description",
                (object?)card.Description ?? DBNull.Value
            );
            return await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
        }

        public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
        {
            await using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = "DELETE FROM Cards WHERE Id = $id;";
            cmd.Parameters.AddWithValue("$id", id);
            return await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
        }

        public async Task SetGamesAsync(
            long cardId,
            IEnumerable<CardGameLink> links,
            CancellationToken cancellationToken = default
        )
        {
            await using var tx = await _db.Connection
                .BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);

            await using (var delete = _db.Connection.CreateCommand())
            {
                delete.Transaction = (SqliteTransaction)tx;
                delete.CommandText = "DELETE FROM CardGames WHERE CardId = $cardId;";
                delete.Parameters.AddWithValue("$cardId", cardId);
                await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using (var insert = _db.Connection.CreateCommand())
            {
                insert.Transaction = (SqliteTransaction)tx;
                insert.CommandText =
                    @"
INSERT INTO CardGames (CardId, LibraryGameId, SortOrder)
VALUES ($cardId, $libraryGameId, $sortOrder);";
                var pCard = insert.Parameters.Add("$cardId", SqliteType.Integer);
                var pGame = insert.Parameters.Add("$libraryGameId", SqliteType.Integer);
                var pSort = insert.Parameters.Add("$sortOrder", SqliteType.Integer);
                pCard.Value = cardId;

                foreach (var link in links)
                {
                    pGame.Value = link.LibraryGameId;
                    pSort.Value = link.SortOrder;
                    await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<LibraryGameRecord>> GetGamesForCardAsync(
            long cardId,
            CancellationToken cancellationToken = default
        )
        {
            var list = new List<LibraryGameRecord>();
            await using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText =
                @"
SELECT g.Id, g.SourcePath, g.Name, g.ProductNumber, g.Disc, g.LengthBytes, g.FileFormat, g.SpecialDisc, g.ReleaseDate, g.Region, g.SyncStatus
FROM CardGames cg
INNER JOIN LibraryGames g ON g.Id = cg.LibraryGameId
WHERE cg.CardId = $cardId
ORDER BY cg.SortOrder, g.Name COLLATE NOCASE;";
            cmd.Parameters.AddWithValue("$cardId", cardId);

            await using var reader = await cmd
                .ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                list.Add(
                    new LibraryGameRecord
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
                    }
                );
            }

            return list;
        }
    }
}
