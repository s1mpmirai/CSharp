using System.Text.Json;
using FoodStreetAudioGuide.Models;

namespace FoodStreetAudioGuide
{
    public class OfflineCacheService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        private readonly string _cacheRoot;
        private readonly string _stallsCacheFile;
        private readonly string _pendingLogsFile;
        private readonly string _imagesDirectory;

        public OfflineCacheService()
        {
            _cacheRoot = Path.Combine(FileSystem.AppDataDirectory, "offline-cache");
            _stallsCacheFile = Path.Combine(_cacheRoot, "stalls.json");
            _pendingLogsFile = Path.Combine(_cacheRoot, "pending-listening-logs.json");
            _imagesDirectory = Path.Combine(_cacheRoot, "images");

            Directory.CreateDirectory(_cacheRoot);
            Directory.CreateDirectory(_imagesDirectory);
        }

        public async Task SaveStallsAsync(IReadOnlyCollection<StallItem> stalls)
        {
            await using var stream = File.Create(_stallsCacheFile);
            await JsonSerializer.SerializeAsync(stream, stalls, JsonOptions);
        }

        public async Task<List<StallItem>> LoadStallsAsync()
        {
            if (!File.Exists(_stallsCacheFile))
            {
                return new List<StallItem>();
            }

            await using var stream = File.OpenRead(_stallsCacheFile);
            var stalls = await JsonSerializer.DeserializeAsync<List<StallItem>>(stream, JsonOptions);
            return stalls ?? new List<StallItem>();
        }

        public async Task<string> SaveImageAsync(int stallId, string originalUrl, byte[] bytes, string variant = "full")
        {
            var extension = GetImageExtension(originalUrl);
            var filePath = Path.Combine(_imagesDirectory, $"stall-{stallId}-{variant}{extension}");
            await File.WriteAllBytesAsync(filePath, bytes);
            return filePath;
        }

        public string? TryGetCachedImagePath(int stallId, string originalUrl, string variant = "full")
        {
            var extension = GetImageExtension(originalUrl);
            var filePath = Path.Combine(_imagesDirectory, $"stall-{stallId}-{variant}{extension}");
            return File.Exists(filePath) ? filePath : null;
        }

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

        public async Task QueueListeningLogAsync(PendingListeningLog log)
        {
            var logs = await LoadPendingListeningLogsAsync();
            logs.Add(log);
            await SavePendingListeningLogsAsync(logs);
        }

        public async Task SavePendingListeningLogsAsync(IReadOnlyCollection<PendingListeningLog> logs)
        {
            await using var stream = File.Create(_pendingLogsFile);
            await JsonSerializer.SerializeAsync(stream, logs, JsonOptions);
        }

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
    }

    public record PendingListeningLog(
        int StallId,
        string LanguageCode,
        int DurationSeconds,
        string Source
    );
}
