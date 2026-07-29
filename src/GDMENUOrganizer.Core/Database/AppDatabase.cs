#nullable enable
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace GDMENUOrganizer.Core.Database
{
    public sealed class AppDatabase : IAsyncDisposable, IDisposable
    {
        public const int CurrentSchemaVersion = 2;

        private static readonly SemaphoreSlim InitLock = new(1, 1);
        private static AppDatabase? _instance;
        private static Task? _ensureTask;

        private readonly SqliteConnection _connection;
        private bool _disposed;

        private AppDatabase(SqliteConnection connection)
        {
            _connection = connection;
        }

        public static AppDatabase Instance =>
            _instance ?? throw new InvalidOperationException("AppDatabase has not been initialized.");

        public static bool IsInitialized => _instance != null;

        public SqliteConnection Connection
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return _connection;
            }
        }

        public PsGameRepository PsGames { get; private set; } = null!;
        public LibraryRepository Library { get; private set; } = null!;
        public CardRepository Cards { get; private set; } = null!;

        /// <summary>
        /// Creates the DB if missing, applies schema, and ensures the PS catalog is imported/refreshed.
        /// Safe to call multiple times; concurrent callers share one init task.
        /// </summary>
        public static Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
        {
            if (_instance != null)
                return RefreshPsCatalogIfNeededAsync(cancellationToken);

            lock (InitLock)
            {
                _ensureTask ??= EnsureCreatedCoreAsync();
            }

            return AwaitEnsureAndMaybeRefreshAsync(cancellationToken);
        }

        private static async Task AwaitEnsureAndMaybeRefreshAsync(CancellationToken cancellationToken)
        {
            await _ensureTask!.ConfigureAwait(false);
            await RefreshPsCatalogIfNeededAsync(cancellationToken).ConfigureAwait(false);
        }

        private static async Task EnsureCreatedCoreAsync()
        {
            await InitLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_instance != null)
                    return;

                var dbPath = AppStorage.DatabasePath;
                var createdNew = !File.Exists(dbPath);

                var connection = new SqliteConnection($"Data Source={dbPath}");
                await connection.OpenAsync().ConfigureAwait(false);

                await using (var pragma = connection.CreateCommand())
                {
                    pragma.CommandText = "PRAGMA foreign_keys = ON;";
                    await pragma.ExecuteNonQueryAsync().ConfigureAwait(false);
                }

                var db = new AppDatabase(connection);
                await db.ApplySchemaAsync().ConfigureAwait(false);
                db.PsGames = new PsGameRepository(db);
                db.Library = new LibraryRepository(db);
                db.Cards = new CardRepository(db);

                _instance = db;

                if (createdNew || await db.PsGames.CountAsync().ConfigureAwait(false) == 0)
                    await PsGamesImporter.EnsureCatalogAsync(db, forceRefresh: true).ConfigureAwait(false);
            }
            finally
            {
                InitLock.Release();
            }
        }

        private static async Task RefreshPsCatalogIfNeededAsync(CancellationToken cancellationToken)
        {
            if (_instance == null)
                return;

            await PsGamesImporter
                .EnsureCatalogAsync(_instance, forceRefresh: false, cancellationToken)
                .ConfigureAwait(false);
        }

        private async Task ApplySchemaAsync()
        {
            var sql = LoadEmbeddedSchema();
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            var versionText = await GetMetaAsync("SchemaVersion").ConfigureAwait(false);
            if (!int.TryParse(versionText, out var version))
                version = 0;

            if (version < 2)
            {
                await MigrateToV2Async().ConfigureAwait(false);
                version = 2;
            }

            if (version < CurrentSchemaVersion)
            {
                await SetMetaAsync("SchemaVersion", CurrentSchemaVersion.ToString())
                    .ConfigureAwait(false);
            }
            else if (versionText != CurrentSchemaVersion.ToString())
            {
                await SetMetaAsync("SchemaVersion", CurrentSchemaVersion.ToString())
                    .ConfigureAwait(false);
            }
        }

        private async Task MigrateToV2Async()
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText =
                @"
SELECT COUNT(1) FROM pragma_table_info('LibraryGames') WHERE name = 'SyncStatus';";
            var hasColumn = Convert.ToInt64(await cmd.ExecuteScalarAsync().ConfigureAwait(false)) > 0;
            if (!hasColumn)
            {
                await using var alter = _connection.CreateCommand();
                alter.CommandText =
                    "ALTER TABLE LibraryGames ADD COLUMN SyncStatus TEXT NOT NULL DEFAULT 'present';";
                await alter.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            await SetMetaAsync("SchemaVersion", "2").ConfigureAwait(false);
        }

        private static string LoadEmbeddedSchema()
        {
            var assembly = typeof(AppDatabase).Assembly;
            const string resourceName = "GDMENUOrganizer.Core.Database.Schema.sql";
            using var stream =
                assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException(
                    $"Embedded resource '{resourceName}' not found."
                );
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        public async Task<string?> GetMetaAsync(string key)
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT Value FROM Meta WHERE Key = $key LIMIT 1;";
            cmd.Parameters.AddWithValue("$key", key);
            var result = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
            return result == null || result == DBNull.Value ? null : Convert.ToString(result);
        }

        public async Task SetMetaAsync(string key, string? value)
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText =
                @"
INSERT INTO Meta (Key, Value) VALUES ($key, $value)
ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;";
            cmd.Parameters.AddWithValue("$key", key);
            cmd.Parameters.AddWithValue("$value", (object?)value ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _connection.Dispose();
            if (ReferenceEquals(_instance, this))
                _instance = null;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;
            _disposed = true;
            await _connection.DisposeAsync().ConfigureAwait(false);
            if (ReferenceEquals(_instance, this))
                _instance = null;
        }
    }
}
