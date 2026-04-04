using FoodStreetAudioGuide.Models;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Maui.Networking;

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

        public async Task<string?> GetPlayableAudioPathAsync(StallItem stall, string languageCode)
        {
            var localPath = GetAudioPath(stall.Id, languageCode, stall.GetScript(languageCode));
            DeleteOlderAudioVariants(stall.Id, languageCode, stall.GetScript(languageCode));
            if (File.Exists(localPath))
            {
                return localPath;
            }

            if (!CanAttemptBackendRequest())
            {
                return null;
            }

            var cacheKey = $"{stall.Id}:{languageCode}:{BuildScriptFingerprint(stall.GetScript(languageCode), languageCode)}";
            var downloadTask = _inflightAudioDownloads.GetOrAdd(
                cacheKey,
                _ => DownloadAudioAsync(localPath, stall.Id, languageCode, stall.GetScript(languageCode)));

            try
            {
                return await downloadTask;
            }
            finally
            {
                _inflightAudioDownloads.TryRemove(cacheKey, out _);
            }
        }

        private async Task<string?> DownloadAudioAsync(string localPath, int stallId, string languageCode, string scriptText)
        {
            try
            {
                using var response = await _httpClient.GetAsync(
                    $"audio/stalls/{stallId}?language_code={Uri.EscapeDataString(languageCode)}",
                    HttpCompletionOption.ResponseHeadersRead);

                if (response.IsSuccessStatusCode)
                {
                    DeleteOlderAudioVariants(stallId, languageCode, scriptText);
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

        public bool HasCachedAudio(StallItem stall, string languageCode)
        {
            DeleteOlderAudioVariants(stall.Id, languageCode, stall.GetScript(languageCode));
            return File.Exists(GetAudioPath(stall.Id, languageCode, stall.GetScript(languageCode)));
        }

        public async Task<bool> PreloadAudioAsync(StallItem stall, string languageCode)
        {
            var path = await GetPlayableAudioPathAsync(stall, languageCode);
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
        }

        public async Task<IReadOnlyList<int>> PreloadTopStallsAsync(IEnumerable<StallItem> stalls, string languageCode, int limit = 5)
        {
            var cachedIds = new List<int>();

            if (!CanAttemptBackendRequest())
            {
                foreach (var stall in stalls.Where(item => item.Id > 0).Take(limit))
                {
                    if (HasCachedAudio(stall, languageCode))
                    {
                        cachedIds.Add(stall.Id);
                    }
                }

                return cachedIds;
            }

            foreach (var stall in stalls.Where(item => item.Id > 0).Take(limit))
            {
                if (HasCachedAudio(stall, languageCode))
                {
                    cachedIds.Add(stall.Id);
                    continue;
                }

                try
                {
                    if (await PreloadAudioAsync(stall, languageCode))
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

        private string GetAudioPath(int stallId, string languageCode, string scriptText)
        {
            var safeLanguageCode = SanitizeLanguageCode(languageCode);
            var scriptFingerprint = BuildScriptFingerprint(scriptText, languageCode);
            return Path.Combine(_audioDirectory, $"stall-{stallId}-{safeLanguageCode}-{_audioProfileVersion}-{scriptFingerprint}.mp3");
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

        private void DeleteOlderAudioVariants(int stallId, string languageCode, string currentScriptText)
        {
            var safeLanguageCode = SanitizeLanguageCode(languageCode);
            var currentSuffix = $"-{_audioProfileVersion}-{BuildScriptFingerprint(currentScriptText, languageCode)}.mp3";
            foreach (var path in Directory.GetFiles(_audioDirectory, $"stall-{stallId}-{safeLanguageCode}-*.mp3"))
            {
                if (!path.EndsWith(currentSuffix, StringComparison.OrdinalIgnoreCase))
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

        private static string SanitizeLanguageCode(string value)
        {
            return SanitizeSegment(value).Replace("-", "_");
        }

        private string BuildScriptFingerprint(string scriptText, string languageCode)
        {
            var normalized = $"{_audioProfileVersion}:{languageCode}:{scriptText ?? string.Empty}";
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
            return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
        }

        private static bool CanAttemptBackendRequest()
        {
            return Connectivity.Current.NetworkAccess != NetworkAccess.None;
        }
    }
}
