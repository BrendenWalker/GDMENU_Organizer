#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ByteSizeLib;
using NiceIO;

namespace GDMENUOrganizer.Core.Database
{
    public sealed class LibraryRefreshResult
    {
        public int PresentCount { get; init; }
        public int NewCount { get; init; }
        public int MissingCount { get; init; }
        public IReadOnlyList<string> Skipped { get; init; } = Array.Empty<string>();
        public IReadOnlyList<LibraryGameRecord> Games { get; init; } =
            Array.Empty<LibraryGameRecord>();
    }

    public static class LibraryScanner
    {
        public static async Task<LibraryRefreshResult> RefreshAsync(
            string libraryPath,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrWhiteSpace(libraryPath))
                throw new ArgumentException("Library path is required.", nameof(libraryPath));

            var root = Path.GetFullPath(libraryPath.Trim());
            if (!Directory.Exists(root))
                throw new DirectoryNotFoundException($"Library folder not found: {root}");

            await AppDatabase.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
            var repo = AppDatabase.Instance.Library;

            var existing = await repo.ListAsync(cancellationToken).ConfigureAwait(false);
            var byPath = existing.ToDictionary(
                x => NormalizePath(x.SourcePath),
                x => x,
                StringComparer.OrdinalIgnoreCase
            );

            var foundPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var skipped = new List<string>();
            var newCount = 0;
            var presentCount = 0;

            foreach (var entry in EnumerateLibraryEntries(root))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var normalized = NormalizePath(entry);

                try
                {
                    var item = await ImageHelper.CreateGdItemAsync(entry).ConfigureAwait(false);
                    if (!HasDeterminedProductNumber(item))
                    {
                        skipped.Add($"{normalized}: No product number");
                        continue;
                    }

                    item.SourcePath = normalized;
                    item.Location = LocationEnum.Other;
                    foundPaths.Add(normalized);

                    var isNew = !byPath.ContainsKey(normalized);
                    var record = ToRecord(
                        item,
                        isNew ? LibrarySyncStatuses.New : LibrarySyncStatuses.Present
                    );
                    await repo.UpsertAsync(record, cancellationToken).ConfigureAwait(false);

                    if (isNew)
                        newCount++;
                    else
                        presentCount++;
                }
                catch (Exception ex)
                {
                    skipped.Add($"{normalized}: {ex.Message}");
                }
            }

            var missingCount = 0;
            foreach (var record in existing)
            {
                if (foundPaths.Contains(NormalizePath(record.SourcePath)))
                    continue;

                await repo
                    .SetSyncStatusAsync(record.Id, LibrarySyncStatuses.Missing, cancellationToken)
                    .ConfigureAwait(false);
                missingCount++;
            }

            var games = await repo.ListAsync(cancellationToken).ConfigureAwait(false);
            return new LibraryRefreshResult
            {
                PresentCount = presentCount,
                NewCount = newCount,
                MissingCount = missingCount,
                Skipped = skipped,
                Games = games
            };
        }

        public static GdItem ToGdItem(LibraryGameRecord record)
        {
            var path = record.SourcePath;
            string folderPath;
            if (Directory.Exists(path))
                folderPath = path;
            else if (File.Exists(path))
                folderPath = Path.GetDirectoryName(path) ?? path;
            else
                folderPath = path;

            FileFormat fileFormat = FileFormat.Uncompressed;
            if (
                !string.IsNullOrEmpty(record.FileFormat)
                && Enum.TryParse(record.FileFormat, ignoreCase: true, out FileFormat parsed)
            )
            {
                fileFormat = parsed;
            }

            SpecialDisc specialDisc = SpecialDisc.None;
            if (Enum.IsDefined(typeof(SpecialDisc), record.SpecialDisc))
                specialDisc = (SpecialDisc)record.SpecialDisc;

            var item = new GdItem
            {
                LibraryGameId = record.Id,
                SourcePath = path,
                FullFolderPath = folderPath,
                Name = record.Name,
                ProductNumber = record.ProductNumber,
                Length = ByteSize.FromBytes(record.LengthBytes),
                FileFormat = fileFormat,
                Location = LocationEnum.Other,
                SyncStatus = record.SyncStatus,
                Ip = new IpBin
                {
                    Disc = record.Disc,
                    Region = record.Region,
                    ReleaseDate = record.ReleaseDate,
                    SpecialDisc = specialDisc,
                    Name = record.Name,
                    ProductNumber = record.ProductNumber
                }
            };

            return item;
        }

        private static bool HasDeterminedProductNumber(GdItem item)
        {
            var productNumber = item.ProductNumber?.Trim();
            if (string.IsNullOrEmpty(productNumber))
                productNumber = item.Ip?.ProductNumber?.Trim();

            return !string.IsNullOrEmpty(productNumber);
        }

        private static LibraryGameRecord ToRecord(GdItem item, string syncStatus)
        {
            return new LibraryGameRecord
            {
                SourcePath = NormalizePath(item.SourcePath?.ToString() ?? item.FullFolderPath.ToString()),
                Name = item.Name,
                ProductNumber = item.ProductNumber,
                Disc = item.Ip?.Disc,
                LengthBytes = (long)item.Length.Bytes,
                FileFormat = item.FileFormat.ToString(),
                SpecialDisc = (int)(item.Ip?.SpecialDisc ?? SpecialDisc.None),
                ReleaseDate = item.Ip?.ReleaseDate,
                Region = item.Ip?.Region,
                SyncStatus = syncStatus
            };
        }

        private static IEnumerable<string> EnumerateLibraryEntries(string libraryPath)
        {
            foreach (var dir in Directory.EnumerateDirectories(libraryPath))
                yield return dir;

            var imageExts = Manager.SupportedImageFormats;
            var compressedExts = Manager.CompressedFileExtensions ?? Array.Empty<string>();

            foreach (var file in Directory.EnumerateFiles(libraryPath))
            {
                var path = new NPath(file);
                if (path.HasExtension(imageExts) || path.HasExtension(compressedExts))
                    yield return file;
            }
        }

        private static string NormalizePath(string path)
        {
            try
            {
                return Path.GetFullPath(path.Trim());
            }
            catch
            {
                return path.Trim();
            }
        }
    }
}
