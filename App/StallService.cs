using System.Diagnostics;
using System.Net.Http.Json;
using FoodStreetAudioGuide.Models;

namespace FoodStreetAudioGuide
{
    public class StallService
    {
        private readonly HttpClient _httpClient;
        private readonly OfflineCacheService _offlineCache;

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
                    var result = await response.Content.ReadFromJsonAsync<List<StallItem>>();
                    var stalls = await CacheImagesAsync(result ?? new List<StallItem>());
                    await _offlineCache.SaveStallsAsync(stalls);
                    await FlushPendingListeningLogsAsync();
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

        private async Task<List<StallItem>> CacheImagesAsync(List<StallItem> stalls)
        {
            var cached = new List<StallItem>(stalls.Count);

            foreach (var stall in stalls)
            {
                if (string.IsNullOrWhiteSpace(stall.ImageUrl))
                {
                    cached.Add(stall);
                    continue;
                }

                try
                {
                    var bytes = await _httpClient.GetByteArrayAsync(stall.ImageUrl);
                    var localPath = await _offlineCache.SaveImageAsync(stall.Id, stall.ImageUrl, bytes);
                    cached.Add(stall with { ImageUrl = localPath });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(@"--- LOI CACHE ANH: " + ex.Message);
                    cached.Add(stall);
                }
            }

            return cached;
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
