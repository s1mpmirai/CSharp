using System.Text.Json;
using FoodStreetAudioGuide.Models;

namespace FoodStreetAudioGuide
{
    public class OfflineCacheService
    {
        // Hàm `new`: xử lý logic liên quan trong file hiện tại.
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        private readonly string _cacheRoot;
        private readonly string _stallsCacheFile;
        private readonly string _pendingLogsFile;
        private readonly string _pendingLocationFile;
        private readonly string _imagesDirectory;
        private readonly string _deviceIdFile;
        private readonly string _sessionIdFile;
        private readonly string _stallsCacheVersionFile;
        private List<StallItem>? _cachedStalls;
        private bool _stallsLoaded;
        private const int CurrentStallsCacheVersion = 2;

        // Hàm khởi tạo `OfflineCacheService`: thiết lập trạng thái ban đầu cho đối tượng trong file hiện tại.
        public OfflineCacheService()
        {
            _cacheRoot = Path.Combine(FileSystem.AppDataDirectory, "offline-cache");
            _stallsCacheFile = Path.Combine(_cacheRoot, "stalls.json");
            _pendingLogsFile = Path.Combine(_cacheRoot, "pending-listening-logs.json");
            _pendingLocationFile = Path.Combine(_cacheRoot, "pending-location-logs.json");
            _imagesDirectory = Path.Combine(_cacheRoot, "images");
            _deviceIdFile = Path.Combine(_cacheRoot, "device-id.txt");
            _sessionIdFile = Path.Combine(_cacheRoot, "session-id.txt");
            _stallsCacheVersionFile = Path.Combine(_cacheRoot, "stalls.version");

            Directory.CreateDirectory(_cacheRoot);
            Directory.CreateDirectory(_imagesDirectory);
        }

        // Hàm `SaveStallsAsync`: lưu dữ liệu hoặc trạng thái liên quan trong file hiện tại.
        public async Task SaveStallsAsync(IReadOnlyCollection<StallItem> stalls)
        {
            await EnsureStallsCacheReadyAsync();
            _cachedStalls = stalls.ToList();
            _stallsLoaded = true;
            await using var stream = File.Create(_stallsCacheFile);
            await JsonSerializer.SerializeAsync(stream, stalls, JsonOptions);
        }

        // Hàm `LoadStallsAsync`: tải dữ liệu hoặc trạng thái cần thiết trong file hiện tại.
        public async Task<List<StallItem>> LoadStallsAsync()
        {
            await EnsureStallsCacheReadyAsync();

            if (_stallsLoaded)
            {
                return _cachedStalls is null ? new List<StallItem>() : new List<StallItem>(_cachedStalls);
            }

            if (!File.Exists(_stallsCacheFile))
            {
                _cachedStalls = new List<StallItem>();
                _stallsLoaded = true;
                return new List<StallItem>();
            }

            try
            {
                await using var stream = File.OpenRead(_stallsCacheFile);
                var stalls = await JsonSerializer.DeserializeAsync<List<StallItem>>(stream, JsonOptions);
                _cachedStalls = stalls ?? new List<StallItem>();
                _stallsLoaded = true;
                return new List<StallItem>(_cachedStalls);
            }
            catch
            {
                await ClearStallsCacheAsync();
                _cachedStalls = new List<StallItem>();
                _stallsLoaded = true;
                return new List<StallItem>();
            }
        }

        // Hàm `ClearStallsCacheAsync`: xử lý logic liên quan trong file hiện tại.
        public async Task ClearStallsCacheAsync()
        {
            if (File.Exists(_stallsCacheFile))
            {
                File.Delete(_stallsCacheFile);
            }

            _cachedStalls = new List<StallItem>();
            _stallsLoaded = false;
            await WriteStallsCacheVersionAsync();
        }

        // Hàm `SaveImageAsync`: lưu dữ liệu hoặc trạng thái liên quan trong file hiện tại.
        public async Task<string> SaveImageAsync(int stallId, string originalUrl, byte[] bytes, string variant = "full")
        {
            var extension = GetImageExtension(originalUrl);
            var filePath = Path.Combine(_imagesDirectory, $"stall-{stallId}-{variant}{extension}");
            await File.WriteAllBytesAsync(filePath, bytes);
            return filePath;
        }

        // Hàm `TryGetCachedImagePath`: xử lý logic liên quan trong file hiện tại.
        public string? TryGetCachedImagePath(int stallId, string originalUrl, string variant = "full")
        {
            var extension = GetImageExtension(originalUrl);
            var filePath = Path.Combine(_imagesDirectory, $"stall-{stallId}-{variant}{extension}");
            return File.Exists(filePath) ? filePath : null;
        }

        // Hàm `LoadPendingListeningLogsAsync`: tải dữ liệu hoặc trạng thái cần thiết trong file hiện tại.
        public async Task<List<PendingListeningLog>> LoadPendingListeningLogsAsync()
        {
            if (!File.Exists(_pendingLogsFile))
            {
                return new List<PendingListeningLog>();
            }

            await using var stream = File.OpenRead(_pendingLogsFile);
            var logs = await JsonSerializer.DeserializeAsync<List<PendingListeningLog>>(stream, JsonOptions);
            return logs ?? new List<PendingListeningLog>();
        }

        // Hàm `QueueListeningLogAsync`: xử lý logic liên quan trong file hiện tại.
        public async Task QueueListeningLogAsync(PendingListeningLog log)
        {
            var logs = await LoadPendingListeningLogsAsync();
            logs.Add(log);
            await SavePendingListeningLogsAsync(logs);
        }

        // Hàm `SavePendingListeningLogsAsync`: lưu dữ liệu hoặc trạng thái liên quan trong file hiện tại.
        public async Task SavePendingListeningLogsAsync(IReadOnlyCollection<PendingListeningLog> logs)
        {
            await using var stream = File.Create(_pendingLogsFile);
            await JsonSerializer.SerializeAsync(stream, logs, JsonOptions);
        }

        // Hàm `LoadPendingLocationLogsAsync`: tải dữ liệu hoặc trạng thái cần thiết trong file hiện tại.
        public async Task<List<PendingLocationLog>> LoadPendingLocationLogsAsync()
        {
            if (!File.Exists(_pendingLocationFile))
            {
                return new List<PendingLocationLog>();
            }

            await using var stream = File.OpenRead(_pendingLocationFile);
            var logs = await JsonSerializer.DeserializeAsync<List<PendingLocationLog>>(stream, JsonOptions);
            return logs ?? new List<PendingLocationLog>();
        }

        // Hàm `QueueLocationLogAsync`: xử lý logic liên quan trong file hiện tại.
        public async Task QueueLocationLogAsync(PendingLocationLog log)
        {
            var logs = await LoadPendingLocationLogsAsync();
            logs.Add(log);
            await SavePendingLocationLogsAsync(logs);
        }

        // Hàm `SavePendingLocationLogsAsync`: lưu dữ liệu hoặc trạng thái liên quan trong file hiện tại.
        public async Task SavePendingLocationLogsAsync(IReadOnlyCollection<PendingLocationLog> logs)
        {
            await using var stream = File.Create(_pendingLocationFile);
            await JsonSerializer.SerializeAsync(stream, logs, JsonOptions);
        }

        // Hàm `GetOrCreateDeviceId`: lấy dữ liệu hoặc giá trị cần dùng trong file hiện tại.
        public string GetOrCreateDeviceId()
        {
            if (File.Exists(_deviceIdFile))
            {
                var existing = File.ReadAllText(_deviceIdFile).Trim();
                if (!string.IsNullOrWhiteSpace(existing))
                {
                    return existing;
                }
            }

            var newValue = Guid.NewGuid().ToString("N");
            File.WriteAllText(_deviceIdFile, newValue);
            return newValue;
        }

        // Hàm `GetOrCreateSessionId`: lấy dữ liệu hoặc giá trị cần dùng trong file hiện tại.
        public string GetOrCreateSessionId()
        {
            if (File.Exists(_sessionIdFile))
            {
                var existing = File.ReadAllText(_sessionIdFile).Trim();
                if (!string.IsNullOrWhiteSpace(existing))
                {
                    return existing;
                }
            }

            var newValue = $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
            File.WriteAllText(_sessionIdFile, newValue);
            return newValue;
        }

        // Hàm `GetImageExtension`: lấy dữ liệu hoặc giá trị cần dùng trong file hiện tại.
        private static string GetImageExtension(string originalUrl)
        {
            if (Uri.TryCreate(originalUrl, UriKind.Absolute, out var uri))
            {
                var extension = Path.GetExtension(uri.AbsolutePath);
                if (!string.IsNullOrWhiteSpace(extension))
                {
                    return extension;
                }
            }

            return ".jpg";
        }

        // Hàm `EnsureStallsCacheReadyAsync`: đảm bảo trạng thái hoặc đăng ký cần thiết trong file hiện tại.
        private async Task EnsureStallsCacheReadyAsync()
        {
            var storedVersion = await ReadStallsCacheVersionAsync();
            if (storedVersion == CurrentStallsCacheVersion)
            {
                return;
            }

            if (File.Exists(_stallsCacheFile))
            {
                File.Delete(_stallsCacheFile);
            }

            _cachedStalls = new List<StallItem>();
            _stallsLoaded = false;
            await WriteStallsCacheVersionAsync();
        }

        // Hàm `ReadStallsCacheVersionAsync`: xử lý logic liên quan trong file hiện tại.
        private async Task<int?> ReadStallsCacheVersionAsync()
        {
            if (!File.Exists(_stallsCacheVersionFile))
            {
                return null;
            }

            var raw = await File.ReadAllTextAsync(_stallsCacheVersionFile);
            return int.TryParse(raw.Trim(), out var version) ? version : null;
        }

        // Hàm `WriteStallsCacheVersionAsync`: xử lý logic liên quan trong file hiện tại.
        private Task WriteStallsCacheVersionAsync()
        {
            return File.WriteAllTextAsync(_stallsCacheVersionFile, CurrentStallsCacheVersion.ToString());
        }
    }

    public record PendingListeningLog(
        int StallId,
        string LanguageCode,
        int DurationSeconds,
        string Source,
        string SessionId,
        string DeviceId,
        double? Latitude,
        double? Longitude
    );

    public record PendingLocationLog(
        string SessionId,
        string DeviceId,
        double Latitude,
        double Longitude,
        string Source,
        DateTime RecordedAtUtc
    );
}
