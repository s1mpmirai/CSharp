using FoodStreetAudioGuide.Models;

namespace FoodStreetAudioGuide
{
    public record DownloadedAudioItem(int StallId, string LanguageCode, string FilePath, DateTime DownloadedAt);

    public class AudioCacheService
    {
        private readonly HttpClient _httpClient;
        private readonly string _audioDirectory;

        public AudioCacheService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _audioDirectory = Path.Combine(FileSystem.AppDataDirectory, "offline-cache", "audio");
            Directory.CreateDirectory(_audioDirectory);
        }

        public async Task<string?> GetPlayableAudioPathAsync(int stallId, string languageCode)
        {
            var localPath = GetAudioPath(stallId, languageCode);

            try
            {
                var response = await _httpClient.GetAsync($"audio/stalls/{stallId}?language_code={Uri.EscapeDataString(languageCode)}");
                if (response.IsSuccessStatusCode)
                {
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

        public async Task PreloadTopStallsAsync(IEnumerable<StallItem> stalls, string languageCode, int limit = 5)
        {
            foreach (var stall in stalls.Where(item => item.Id > 0).Take(limit))
            {
                if (HasCachedAudio(stall.Id, languageCode))
                {
                    continue;
                }

                try
                {
                    await PreloadAudioAsync(stall.Id, languageCode);
                }
                catch
                {
                    // Ignore preload failures. Popup can still use TTS fallback later.
                }
            }
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
                    var parts = name.Split('-', 3);
                    var stallId = parts.Length >= 2 && int.TryParse(parts[1], out var parsedId) ? parsedId : 0;
                    var languageCode = parts.Length == 3 ? parts[2] : "unknown";
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
            var safeLanguageCode = languageCode.Replace("/", "-").Replace("\\", "-");
            return Path.Combine(_audioDirectory, $"stall-{stallId}-{safeLanguageCode}.mp3");
        }
    }
}
