#nullable enable
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace GDMENUOrganizer.Core.Database
{
    public enum PsGamesDownloadStatus
    {
        Downloaded,
        NotModified,
        Failed
    }

    public sealed class PsGamesDownloadResult
    {
        public PsGamesDownloadStatus Status { get; init; }
        public string? ETag { get; init; }
        public string? LocalYamlPath { get; init; }
        public string? ErrorMessage { get; init; }
    }

    public static class PsGamesDownloader
    {
        private static readonly HttpClient Http = CreateClient();

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                $"GDMENUOrganizer/{Constants.Version}"
            );
            return client;
        }

        public static async Task<PsGamesDownloadResult> TryDownloadAsync(
            string? existingETag,
            CancellationToken cancellationToken = default
        )
        {
            try
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    Constants.DuckStationGameDbYamlUrl
                );
                if (!string.IsNullOrWhiteSpace(existingETag))
                {
                    if (EntityTagHeaderValue.TryParse(existingETag, out var tag))
                        request.Headers.IfNoneMatch.Add(tag);
                    else if (
                        EntityTagHeaderValue.TryParse("\"" + existingETag.Trim('"') + "\"", out tag)
                    )
                        request.Headers.IfNoneMatch.Add(tag);
                }

                using var response = await Http
                    .SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken
                    )
                    .ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    return new PsGamesDownloadResult
                    {
                        Status = PsGamesDownloadStatus.NotModified,
                        ETag = existingETag
                    };
                }

                if (!response.IsSuccessStatusCode)
                {
                    return new PsGamesDownloadResult
                    {
                        Status = PsGamesDownloadStatus.Failed,
                        ErrorMessage = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}"
                    };
                }

                var etag = response.Headers.ETag?.Tag;
                var cachePath = AppStorage.CachedGameDbYamlPath;
                await using (
                    var file = new FileStream(
                        cachePath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None
                    )
                )
                {
                    await response.Content.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
                }

                return new PsGamesDownloadResult
                {
                    Status = PsGamesDownloadStatus.Downloaded,
                    ETag = etag,
                    LocalYamlPath = cachePath
                };
            }
            catch (Exception ex)
            {
                return new PsGamesDownloadResult
                {
                    Status = PsGamesDownloadStatus.Failed,
                    ErrorMessage = ex.Message
                };
            }
        }
    }
}
