using System.Diagnostics;
using System.Net.Http.Json;
using FoodStreetAudioGuide.Models;

namespace FoodStreetAudioGuide
{
    public class StallService
    {
        private readonly HttpClient _httpClient;
        private readonly OfflineCacheService _offlineCache;
        public event Action<IReadOnlyList<StallItem>>? ImageCacheUpdated;

        public StallService(HttpClient httpClient, OfflineCacheService offlineCache)
        {
            _httpClient = httpClient;
            _offlineCache = offlineCache;
        }

        public async Task<List<StallItem>> GetNearbyStallsAsync(double lat, double lng)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("nearby", new { lat, lng });

                if (response.IsSuccessStatusCode)
                {
                    var sourceStalls = await response.Content.ReadFromJsonAsync<List<StallItem>>() ?? new List<StallItem>();
                    var stalls = NormalizeStalls(sourceStalls);
                    await _offlineCache.SaveStallsAsync(stalls);
                    _ = Task.Run(() => PrimeImageCacheAsync(sourceStalls));
                    _ = Task.Run(FlushPendingListeningLogsAsync);
                    return stalls;
                }

                Debug.WriteLine($"--- API Tra ve loi: {response.StatusCode}");
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine(@"--- LOI KET NOI (Kiem tra Docker/Backend): " + ex.Message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(@"--- LOI HE THONG: " + ex.Message);
            }

            return await _offlineCache.LoadStallsAsync();
        }

        public Task<List<StallItem>> LoadCachedStallsAsync()
        {
            return _offlineCache.LoadStallsAsync();
        }

        public async Task<List<StallItem>> SearchStallsAsync(
            string query,
            double? lat = null,
            double? lng = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return new List<StallItem>();
            }

            try
            {
                var response = await _httpClient.PostAsJsonAsync(
                    "search",
                    new
                    {
                        query,
                        lat,
                        lng,
                        limit = 20
                    },
                    cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var sourceStalls = await response.Content.ReadFromJsonAsync<List<StallItem>>(cancellationToken: cancellationToken) ?? new List<StallItem>();
                    return NormalizeStalls(sourceStalls);
                }

                Debug.WriteLine($"--- SEARCH API Tra ve loi: {response.StatusCode}");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine(@"--- LOI TIM KIEM KET NOI: " + ex.Message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(@"--- LOI TIM KIEM HE THONG: " + ex.Message);
            }

            return new List<StallItem>();
        }

        public async Task LogListeningAsync(int stallId, string languageCode, int durationSeconds)
        {
            if (stallId <= 0 || string.IsNullOrWhiteSpace(languageCode))
            {
                return;
            }

            try
            {
                using var formData = BuildListeningLogFormData(stallId, languageCode, durationSeconds, "app");
                var response = await _httpClient.PostAsync("logs/listening", formData);
                if (!response.IsSuccessStatusCode)
                {
                    await QueueListeningLogAsync(stallId, languageCode, durationSeconds);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(@"--- LOI GHI LOG NGHE: " + ex.Message);
                await QueueListeningLogAsync(stallId, languageCode, durationSeconds);
            }
        }

        private List<StallItem> NormalizeStalls(List<StallItem> stalls)
        {
            var normalized = new List<StallItem>(stalls.Count);

            foreach (var stall in stalls)
            {
                if (string.IsNullOrWhiteSpace(stall.ImageUrl))
                {
                    normalized.Add(stall);
                    continue;
                }

                var absoluteUrl = BuildAbsoluteUrl(stall.ImageUrl);
                var thumbnailUrl = string.IsNullOrWhiteSpace(stall.ThumbnailUrl)
                    ? absoluteUrl
                    : BuildAbsoluteUrl(stall.ThumbnailUrl);
                var cachedThumbnailPath = _offlineCache.TryGetCachedImagePath(stall.Id, thumbnailUrl, "thumb");
                var cachedFullPath = _offlineCache.TryGetCachedImagePath(stall.Id, absoluteUrl, "full");

                normalized.Add(stall with
                {
                    ThumbnailUrl = cachedThumbnailPath ?? thumbnailUrl,
                    ImageUrl = cachedFullPath ?? absoluteUrl
                });
            }

            return normalized;
        }

        private async Task PrimeImageCacheAsync(List<StallItem> stalls)
        {
            var changed = 0;

            var tasks = stalls
                .Where(stall => stall.Id > 0 && !string.IsNullOrWhiteSpace(stall.ImageUrl))
                .Select(async stall =>
                {
                    var thumbnailUrl = string.IsNullOrWhiteSpace(stall.ThumbnailUrl)
                        ? BuildAbsoluteUrl(stall.ImageUrl)
                        : BuildAbsoluteUrl(stall.ThumbnailUrl);

                    try
                    {
                        if (_offlineCache.TryGetCachedImagePath(stall.Id, thumbnailUrl, "thumb") is null)
                        {
                            var thumbnailBytes = await _httpClient.GetByteArrayAsync(thumbnailUrl);
                            await _offlineCache.SaveImageAsync(stall.Id, thumbnailUrl, thumbnailBytes, "thumb");
                            Interlocked.Exchange(ref changed, 1);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(@"--- LOI CACHE ANH: " + ex.Message);
                    }
                })
                .ToArray();

            await Task.WhenAll(tasks);

            if (changed == 1)
            {
                var refreshed = NormalizeStalls(stalls);
                await _offlineCache.SaveStallsAsync(refreshed);
                ImageCacheUpdated?.Invoke(refreshed);
            }
        }

        private string BuildAbsoluteUrl(string imageUrl)
        {
            if (Uri.TryCreate(imageUrl, UriKind.Absolute, out _))
            {
                return imageUrl;
            }

            if (_httpClient.BaseAddress is null)
            {
                return imageUrl;
            }

            return new Uri(_httpClient.BaseAddress, imageUrl.TrimStart('/')).ToString();
        }

        private async Task QueueListeningLogAsync(int stallId, string languageCode, int durationSeconds)
        {
            await _offlineCache.QueueListeningLogAsync(new PendingListeningLog(
                stallId,
                languageCode,
                Math.Max(durationSeconds, 0),
                "app"));
        }

        private async Task FlushPendingListeningLogsAsync()
        {
            var pendingLogs = await _offlineCache.LoadPendingListeningLogsAsync();
            if (pendingLogs.Count == 0)
            {
                return;
            }

            var remainingLogs = new List<PendingListeningLog>();

            foreach (var log in pendingLogs)
            {
                try
                {
                    using var formData = BuildListeningLogFormData(log.StallId, log.LanguageCode, log.DurationSeconds, log.Source);
                    var response = await _httpClient.PostAsync("logs/listening", formData);
                    if (!response.IsSuccessStatusCode)
                    {
                        remainingLogs.Add(log);
                    }
                }
                catch
                {
                    remainingLogs.Add(log);
                }
            }

            await _offlineCache.SavePendingListeningLogsAsync(remainingLogs);
        }

        private static MultipartFormDataContent BuildListeningLogFormData(
            int stallId,
            string languageCode,
            int durationSeconds,
            string source)
        {
            return new MultipartFormDataContent
            {
                { new StringContent(stallId.ToString()), "stall_id" },
                { new StringContent(languageCode), "language_code" },
                { new StringContent(Math.Max(durationSeconds, 0).ToString()), "duration_seconds" },
                { new StringContent(source), "source" }
            };
        }
    }
}
