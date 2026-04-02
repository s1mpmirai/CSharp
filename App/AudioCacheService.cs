using FoodStreetAudioGuide.Models;
using System.Collections.Concurrent;

namespace FoodStreetAudioGuide
{
    public record DownloadedAudioItem(int StallId, string LanguageCode, string FilePath, DateTime DownloadedAt);

    public class AudioCacheService
    {
        private readonly HttpClient _httpClient;
        private readonly string _audioDirectory;
        private readonly ConcurrentDictionary<string, Task<string?>> _inflightAudioDownloads = new();
        private readonly string _audioProfileVersion;

        public AudioCacheService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _audioDirectory = Path.Combine(FileSystem.AppDataDirectory, "offline-cache", "audio");
            _audioProfileVersion = SanitizeSegment(AudioSettings.BackendAudioProfileVersion);
            Directory.CreateDirectory(_audioDirectory);
            DeleteStaleAudioFiles();
        }

        public async Task<string?> GetPlayableAudioPathAsync(int stallId, string languageCode)
        {
            var localPath = GetAudioPath(stallId, languageCode);
            if (File.Exists(localPath))
            {
                return localPath;
            }

            var cacheKey = $"{stallId}:{languageCode}";
            var downloadTask = _inflightAudioDownloads.GetOrAdd(
                cacheKey,
                _ => DownloadAudioAsync(localPath, stallId, languageCode));

            try
            {
                return await downloadTask;
            }
            finally
            {
                _inflightAudioDownloads.TryRemove(cacheKey, out _);
            }
        }

        private async Task<string?> DownloadAudioAsync(string localPath, int stallId, string languageCode)
        {
            try
            {
                using var response = await _httpClient.GetAsync(
                    $"audio/stalls/{stallId}?language_code={Uri.EscapeDataString(languageCode)}",
                    HttpCompletionOption.ResponseHeadersRead);

                if (response.IsSuccessStatusCode)
                {
                    DeleteOlderAudioVariants(stallId, languageCode);
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(localPath, bytes);
                    return localPath;
                }
            }
            catch
            {
                // Fall back to cached file when offline.
            }

            return File.Exists(localPath) ? localPath : null;
        }

        public bool HasCachedAudio(int stallId, string languageCode)
        {
            return File.Exists(GetAudioPath(stallId, languageCode));
        }

        public async Task<bool> PreloadAudioAsync(int stallId, string languageCode)
        {
            var path = await GetPlayableAudioPathAsync(stallId, languageCode);
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
        }

        public async Task<IReadOnlyList<int>> PreloadTopStallsAsync(IEnumerable<StallItem> stalls, string languageCode, int limit = 5)
        {
            var cachedIds = new List<int>();

            foreach (var stall in stalls.Where(item => item.Id > 0).Take(limit))
            {
                if (HasCachedAudio(stall.Id, languageCode))
                {
                    cachedIds.Add(stall.Id);
                    continue;
                }

                try
                {
                    if (await PreloadAudioAsync(stall.Id, languageCode))
                    {
                        cachedIds.Add(stall.Id);
                    }
                }
                catch
                {
                    // Ignore preload failures. Popup can still use TTS fallback later.
                }
            }

            return cachedIds;
        }

        public List<DownloadedAudioItem> GetDownloadedAudioItems()
        {
            if (!Directory.Exists(_audioDirectory))
            {
                return new List<DownloadedAudioItem>();
            }

            return Directory
                .GetFiles(_audioDirectory, "stall-*-*.mp3")
                .Select(path =>
                {
                    var name = Path.GetFileNameWithoutExtension(path);
                    var parts = name.Split('-', 4);
                    var stallId = parts.Length >= 2 && int.TryParse(parts[1], out var parsedId) ? parsedId : 0;
                    var languageCode = parts.Length >= 4 ? parts[2] : "unknown";
                    return new DownloadedAudioItem(stallId, languageCode, path, File.GetLastWriteTime(path));
                })
                .OrderByDescending(item => item.DownloadedAt)
                .ToList();
        }

        public bool DeleteCachedAudio(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return false;
            }

            File.Delete(filePath);
            return true;
        }

        private string GetAudioPath(int stallId, string languageCode)
        {
            var safeLanguageCode = SanitizeSegment(languageCode);
            return Path.Combine(_audioDirectory, $"stall-{stallId}-{safeLanguageCode}-{_audioProfileVersion}.mp3");
        }

        private void DeleteStaleAudioFiles()
        {
            foreach (var path in Directory.GetFiles(_audioDirectory, "stall-*-*.mp3"))
            {
                var name = Path.GetFileNameWithoutExtension(path);
                if (!name.EndsWith("-" + _audioProfileVersion, StringComparison.OrdinalIgnoreCase))
                {
                    TryDelete(path);
                }
            }
        }

        private void DeleteOlderAudioVariants(int stallId, string languageCode)
        {
            var safeLanguageCode = SanitizeSegment(languageCode);
            foreach (var path in Directory.GetFiles(_audioDirectory, $"stall-{stallId}-{safeLanguageCode}-*.mp3"))
            {
                if (!path.EndsWith($"-{_audioProfileVersion}.mp3", StringComparison.OrdinalIgnoreCase))
                {
                    TryDelete(path);
                }
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                // Keep going if an old cache file cannot be removed.
            }
        }

        private static string SanitizeSegment(string value)
        {
            return value.Replace("/", "-").Replace("\\", "-").Replace(":", "-").Trim();
        }
    }
}
