using System.Diagnostics;
using System.Net.Http.Json;
using FoodStreetAudioGuide.Models;
using Microsoft.Maui.Networking;

namespace FoodStreetAudioGuide
{
    public class StallService
    {
        private static readonly TimeSpan MapCacheLifetime = TimeSpan.FromSeconds(45);
        private readonly HttpClient _httpClient;
        private readonly OfflineCacheService _offlineCache;
        private List<StallItem>? _cachedMapStalls;
        private DateTime _cachedMapStallsAtUtc;
        public event Action<IReadOnlyList<StallItem>>? ImageCacheUpdated;

        public StallService(HttpClient httpClient, OfflineCacheService offlineCache)
        {
            _httpClient = httpClient;
            _offlineCache = offlineCache;
        }

        public async Task<List<StallItem>> GetNearbyStallsAsync(double lat, double lng)
        {
            if (!CanAttemptBackendRequest())
            {
                return await _offlineCache.LoadStallsAsync();
            }

            try
            {
                var response = await _httpClient.PostAsJsonAsync("nearby", new { lat, lng });

                if (response.IsSuccessStatusCode)
                {
                    var sourceStalls = await response.Content.ReadFromJsonAsync<List<StallItem>>() ?? new List<StallItem>();
                    var stalls = NormalizeStalls(sourceStalls);
                    CacheMapStalls(stalls);
                    await _offlineCache.SaveStallsAsync(stalls);
                    _ = Task.Run(() => PrimeImageCacheAsync(sourceStalls));
                    _ = Task.Run(FlushPendingListeningLogsAsync);
                    _ = Task.Run(FlushPendingLocationLogsAsync);
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

        public async Task<List<StallItem>> GetMapStallsAsync(double? lat = null, double? lng = null)
        {
            if (TryGetFreshMapCache(out var cachedMapStalls))
            {
                return cachedMapStalls;
            }

            if (!CanAttemptBackendRequest())
            {
                var offlineStalls = await _offlineCache.LoadStallsAsync();
                CacheMapStalls(offlineStalls);
                return offlineStalls;
            }

            try
            {
                var path = "stalls/map";
                var query = new List<string>();

                if (lat.HasValue)
                {
                    query.Add($"lat={lat.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                }

                if (lng.HasValue)
                {
                    query.Add($"lng={lng.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                }

                if (query.Count > 0)
                {
                    path += "?" + string.Join("&", query);
                }

                var response = await _httpClient.GetAsync(path);
                if (response.IsSuccessStatusCode)
                {
                    var sourceStalls = await response.Content.ReadFromJsonAsync<List<StallItem>>() ?? new List<StallItem>();
                    var stalls = NormalizeStalls(sourceStalls);
                    CacheMapStalls(stalls);
                    return stalls;
                }

                Debug.WriteLine($"--- MAP API Tra ve loi: {response.StatusCode}");
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine(@"--- LOI MAP KET NOI: " + ex.Message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(@"--- LOI MAP HE THONG: " + ex.Message);
            }

            var fallbackStalls = await _offlineCache.LoadStallsAsync();
            CacheMapStalls(fallbackStalls);
            return fallbackStalls;
        }

        public StallItem? TryResolveQrLocally(string qrCodeValue, IEnumerable<StallItem> candidates)
        {
            if (!TryExtractQrStallId(qrCodeValue, out var stallId))
            {
                return null;
            }

            return candidates.FirstOrDefault(item => item.Id == stallId);
        }

        public Task<List<StallItem>> LoadCachedStallsAsync()
        {
            return _offlineCache.LoadStallsAsync();
        }

        public async Task<string?> GetSyncVersionAsync(CancellationToken cancellationToken = default)
        {
            if (!CanAttemptBackendRequest())
            {
                return null;
            }

            try
            {
                var response = await _httpClient.GetAsync("sync/version", cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var payload = await response.Content.ReadFromJsonAsync<SyncVersionResponse>(cancellationToken: cancellationToken);
                return string.IsNullOrWhiteSpace(payload?.Version) ? null : payload.Version;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return null;
            }
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

            if (!CanAttemptBackendRequest())
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

        public async Task<StallItem?> ResolveQrAsync(string qrCodeValue, double? lat = null, double? lng = null)
        {
            if (string.IsNullOrWhiteSpace(qrCodeValue))
            {
                return null;
            }

            if (!CanAttemptBackendRequest())
            {
                return null;
            }

            try
            {
                var path = $"qr/resolve?code={Uri.EscapeDataString(qrCodeValue)}";
                if (lat.HasValue)
                {
                    path += $"&lat={lat.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                }

                if (lng.HasValue)
                {
                    path += $"&lng={lng.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                }

                var response = await _httpClient.GetAsync(path);
                if (response.IsSuccessStatusCode)
                {
                    var stall = await response.Content.ReadFromJsonAsync<StallItem>();
                    if (stall is null)
                    {
                        return null;
                    }

                    var normalized = NormalizeStalls(new List<StallItem> { stall }).FirstOrDefault();
                    if (normalized is not null)
                    {
                        MergeIntoMapCache(normalized);
                    }

                    return normalized;
                }

                Debug.WriteLine($"--- QR API Tra ve loi: {response.StatusCode}");
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine(@"--- LOI QR KET NOI: " + ex.Message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(@"--- LOI QR HE THONG: " + ex.Message);
            }

            return null;
        }

        public async Task<StallItem?> GetStallDetailAsync(int stallId, double? lat = null, double? lng = null)
        {
            if (stallId <= 0 || !CanAttemptBackendRequest())
            {
                return null;
            }

            try
            {
                var path = $"stalls/{stallId}";
                var query = new List<string>();

                if (lat.HasValue)
                {
                    query.Add($"lat={lat.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                }

                if (lng.HasValue)
                {
                    query.Add($"lng={lng.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                }

                if (query.Count > 0)
                {
                    path += "?" + string.Join("&", query);
                }

                var response = await _httpClient.GetAsync(path);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var stall = await response.Content.ReadFromJsonAsync<StallItem>();
                if (stall is null)
                {
                    return null;
                }

                var normalized = NormalizeStalls(new List<StallItem> { stall }).FirstOrDefault();
                if (normalized is null)
                {
                    return null;
                }

                MergeIntoMapCache(normalized);
                await MergeStallIntoOfflineCacheAsync(normalized);
                return normalized;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(@"--- LOI STALL DETAIL: " + ex.Message);
                return null;
            }
        }

        public async Task<StallItem?> SubmitRatingAsync(int stallId, int rating, Location? location = null)
        {
            if (stallId <= 0 || rating is < 1 or > 5 || !CanAttemptBackendRequest())
            {
                return null;
            }

            try
            {
                var response = await _httpClient.PostAsJsonAsync($"stalls/{stallId}/reviews", new
                {
                    rating,
                    lat = location?.Latitude,
                    lng = location?.Longitude
                });
                if (!response.IsSuccessStatusCode)
                {
                    var error = await TryReadErrorDetailAsync(response);
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "Không thể gửi đánh giá." : error);
                }

                var stall = await response.Content.ReadFromJsonAsync<StallItem>();
                if (stall is null)
                {
                    return null;
                }

                var normalized = NormalizeStalls(new List<StallItem> { stall }).FirstOrDefault();
                if (normalized is null)
                {
                    return null;
                }

                MergeIntoMapCache(normalized);
                await MergeStallIntoOfflineCacheAsync(normalized);
                return normalized;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(@"--- LOI GUI DANH GIA: " + ex.Message);
                throw;
            }
        }

        private bool TryGetFreshMapCache(out List<StallItem> stalls)
        {
            if (_cachedMapStalls is { Count: > 0 } &&
                DateTime.UtcNow - _cachedMapStallsAtUtc <= MapCacheLifetime)
            {
                stalls = new List<StallItem>(_cachedMapStalls);
                return true;
            }

            stalls = new List<StallItem>();
            return false;
        }

        private void CacheMapStalls(List<StallItem> stalls)
        {
            if (stalls.Count == 0)
            {
                return;
            }

            _cachedMapStalls = new List<StallItem>(stalls);
            _cachedMapStallsAtUtc = DateTime.UtcNow;
        }

        private void MergeIntoMapCache(StallItem stall)
        {
            if (_cachedMapStalls is null || _cachedMapStalls.Count == 0)
            {
                CacheMapStalls(new List<StallItem> { stall });
                return;
            }

            var index = _cachedMapStalls.FindIndex(item => item.Id == stall.Id);
            if (index >= 0)
            {
                _cachedMapStalls[index] = stall;
            }
            else
            {
                _cachedMapStalls.Add(stall);
            }

            _cachedMapStallsAtUtc = DateTime.UtcNow;
        }

        private static bool TryExtractQrStallId(string qrCodeValue, out int stallId)
        {
            stallId = 0;
            if (string.IsNullOrWhiteSpace(qrCodeValue))
            {
                return false;
            }

            var parts = qrCodeValue.Trim().Split('.');
            if (parts.Length < 3 || !string.Equals(parts[0], "sfqr1", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return int.TryParse(parts[1], out stallId) && stallId > 0;
        }

        private sealed class SyncVersionResponse
        {
            public string? Version { get; set; }
        }

        public async Task LogListeningAsync(int stallId, string languageCode, int durationSeconds, Location? location = null)
        {
            if (stallId <= 0 || string.IsNullOrWhiteSpace(languageCode))
            {
                return;
            }

            try
            {
                if (!CanAttemptBackendRequest())
                {
                    await QueueListeningLogAsync(stallId, languageCode, durationSeconds, location);
                    return;
                }

                using var formData = BuildListeningLogFormData(stallId, languageCode, durationSeconds, "app", location);
                var response = await _httpClient.PostAsync("logs/listening", formData);
                if (!response.IsSuccessStatusCode)
                {
                    await QueueListeningLogAsync(stallId, languageCode, durationSeconds, location);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(@"--- LOI GHI LOG NGHE: " + ex.Message);
                await QueueListeningLogAsync(stallId, languageCode, durationSeconds, location);
            }
        }

        public async Task LogLocationPingAsync(Location location)
        {
            try
            {
                if (!CanAttemptBackendRequest())
                {
                    await QueueLocationLogAsync(location);
                    return;
                }

                using var formData = BuildLocationLogFormData(location, "app");
                var response = await _httpClient.PostAsync("logs/location", formData);
                if (!response.IsSuccessStatusCode)
                {
                    await QueueLocationLogAsync(location);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(@"--- LOI GHI LOG VI TRI: " + ex.Message);
                await QueueLocationLogAsync(location);
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

        private async Task MergeStallIntoOfflineCacheAsync(StallItem refreshedStall)
        {
            try
            {
                var cached = await _offlineCache.LoadStallsAsync();
                if (cached.Count == 0)
                {
                    return;
                }

                var index = cached.FindIndex(item => item.Id == refreshedStall.Id);
                if (index < 0)
                {
                    return;
                }

                cached[index] = refreshedStall;
                await _offlineCache.SaveStallsAsync(cached);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(@"--- LOI MERGE CACHE STALL DETAIL: " + ex.Message);
            }
        }

        private static async Task<string?> TryReadErrorDetailAsync(HttpResponseMessage response)
        {
            try
            {
                var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
                if (payload is not null && payload.TryGetValue("detail", out var detail))
                {
                    return detail?.ToString();
                }
            }
            catch
            {
            }

            try
            {
                return await response.Content.ReadAsStringAsync();
            }
            catch
            {
                return null;
            }
        }

        private async Task QueueListeningLogAsync(int stallId, string languageCode, int durationSeconds, Location? location)
        {
            var sessionId = _offlineCache.GetOrCreateSessionId();
            var deviceId = _offlineCache.GetOrCreateDeviceId();
            await _offlineCache.QueueListeningLogAsync(new PendingListeningLog(
                stallId,
                languageCode,
                Math.Max(durationSeconds, 0),
                "app",
                sessionId,
                deviceId,
                location?.Latitude,
                location?.Longitude));
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
                    using var formData = BuildListeningLogFormData(
                        log.StallId,
                        log.LanguageCode,
                        log.DurationSeconds,
                        log.Source,
                        log.SessionId,
                        log.DeviceId,
                        log.Latitude,
                        log.Longitude);
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

        private async Task QueueLocationLogAsync(Location location)
        {
            await _offlineCache.QueueLocationLogAsync(new PendingLocationLog(
                _offlineCache.GetOrCreateSessionId(),
                _offlineCache.GetOrCreateDeviceId(),
                location.Latitude,
                location.Longitude,
                "app",
                DateTime.UtcNow));
        }

        private async Task FlushPendingLocationLogsAsync()
        {
            var pendingLogs = await _offlineCache.LoadPendingLocationLogsAsync();
            if (pendingLogs.Count == 0)
            {
                return;
            }

            var remainingLogs = new List<PendingLocationLog>();

            foreach (var log in pendingLogs)
            {
                try
                {
                    using var formData = BuildLocationLogFormData(log);
                    var response = await _httpClient.PostAsync("logs/location", formData);
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

            await _offlineCache.SavePendingLocationLogsAsync(remainingLogs);
        }

        private static MultipartFormDataContent BuildListeningLogFormData(
            int stallId,
            string languageCode,
            int durationSeconds,
            string source,
            Location? location)
        {
            return BuildListeningLogFormData(
                stallId,
                languageCode,
                durationSeconds,
                source,
                string.Empty,
                string.Empty,
                location?.Latitude,
                location?.Longitude);
        }

        private static MultipartFormDataContent BuildListeningLogFormData(
            int stallId,
            string languageCode,
            int durationSeconds,
            string source,
            string sessionId,
            string deviceId,
            double? latitude,
            double? longitude)
        {
            var formData = new MultipartFormDataContent
            {
                { new StringContent(stallId.ToString()), "stall_id" },
                { new StringContent(languageCode), "language_code" },
                { new StringContent(Math.Max(durationSeconds, 0).ToString()), "duration_seconds" },
                { new StringContent(source), "source" }
            };

            if (!string.IsNullOrWhiteSpace(sessionId))
            {
                formData.Add(new StringContent(sessionId), "session_id");
            }

            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                formData.Add(new StringContent(deviceId), "device_id");
            }

            if (latitude.HasValue)
            {
                formData.Add(
                    new StringContent(latitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    "lat");
            }

            if (longitude.HasValue)
            {
                formData.Add(
                    new StringContent(longitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    "lng");
            }

            return formData;
        }

        private MultipartFormDataContent BuildLocationLogFormData(Location location, string source)
        {
            return BuildLocationLogFormData(new PendingLocationLog(
                _offlineCache.GetOrCreateSessionId(),
                _offlineCache.GetOrCreateDeviceId(),
                location.Latitude,
                location.Longitude,
                source,
                DateTime.UtcNow));
        }

        private static MultipartFormDataContent BuildLocationLogFormData(PendingLocationLog log)
        {
            return new MultipartFormDataContent
            {
                { new StringContent(log.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)), "lat" },
                { new StringContent(log.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)), "lng" },
                { new StringContent(log.SessionId), "session_id" },
                { new StringContent(log.DeviceId), "device_id" },
                { new StringContent(log.Source), "source" },
                { new StringContent(log.RecordedAtUtc.ToString("O", System.Globalization.CultureInfo.InvariantCulture)), "recorded_at" }
            };
        }

        private static bool CanAttemptBackendRequest()
        {
            return Connectivity.Current.NetworkAccess != NetworkAccess.None;
        }
    }
}
