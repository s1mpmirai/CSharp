using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using CommunityToolkit.Maui.Views;
using FoodStreetAudioGuide.Models;
using Microsoft.Maui.Controls.Shapes;
using MauiTextToSpeech = Microsoft.Maui.Media.TextToSpeech;
#if ANDROID
using AndroidTtsLanguageAvailableResult = Android.Speech.Tts.LanguageAvailableResult;
using Android.Media;
using AndroidTtsOperationResult = Android.Speech.Tts.OperationResult;
using AndroidTtsQueueMode = Android.Speech.Tts.QueueMode;
using AndroidTextToSpeech = Android.Speech.Tts.TextToSpeech;
using AndroidUtteranceProgressListener = Android.Speech.Tts.UtteranceProgressListener;
using AndroidLocale = Java.Util.Locale;
#endif

namespace FoodStreetAudioGuide
{
    public partial class MainPage : ContentPage
    {
        private readonly StallService _stallService;
        private readonly AudioCacheService _audioCacheService;
        private StallItem? _currentPopupStall;
        private string _selectedLanguage;
        private string _awayLabel = "AWAY";
        private CancellationTokenSource? _speechCts;
        private CancellationTokenSource? _poiMonitorCts;
        private readonly HashSet<int> _poiInsideStalls = new();
        private bool _hasLoadedInitially;
        private static readonly TimeSpan PoiMonitorInitialDelay = TimeSpan.FromSeconds(4);
        private static readonly TimeSpan PoiMonitorInterval = TimeSpan.FromSeconds(20);
        private bool _isSubscribedToImageCacheUpdates;
        private List<StallItem> _nearbyStalls = new();
        private List<StallItem> _remoteSearchStalls = new();
        private string _searchText = string.Empty;
        private string _lastRemoteSearchText = string.Empty;
        private CancellationTokenSource? _searchDebounceCts;
        private Location? _lastKnownLocation;
#if ANDROID
        private MediaPlayer? _androidMediaPlayer;
        private AndroidTextToSpeech? _androidTts;
        private TaskCompletionSource<bool>? _androidTtsInitTcs;
        private TaskCompletionSource<bool>? _androidTtsSpeakTcs;
        private string? _androidTtsUtteranceId;
#endif

        public RangeObservableCollection<StallItem> Stalls { get; } = new();

        public string AwayLabel
        {
            get => _awayLabel;
            set
            {
                if (_awayLabel == value)
                {
                    return;
                }

                _awayLabel = value;
                OnPropertyChanged();
            }
        }

        public Command BackCommand { get; }

        public MainPage(
            StallService stallService,
            AudioCacheService audioCacheService,
            string selectedLanguage = AppText.Vietnamese)
        {
            InitializeComponent();

            _stallService = stallService;
            _audioCacheService = audioCacheService;
            _selectedLanguage = selectedLanguage;

            BackCommand = new Command(async () => await NavigateBackAsync());

            BindingContext = this;
            StallsCollectionView.ItemsSource = Stalls;

            ApplyLanguage(_selectedLanguage);
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            EnsureImageCacheSubscription();
            if (_hasLoadedInitially)
            {
                return;
            }

            _hasLoadedInitially = true;
            ShowInitialLoading();
            await Task.Yield();
            await LoadCachedDataAsync();
            if (Stalls.Count > 0)
            {
                HideInitialLoading();
            }

            _ = LoadDataFromServer();
        }

        protected override void OnDisappearing()
        {
            RemoveImageCacheSubscription();
            _searchDebounceCts?.Cancel();
            _searchDebounceCts?.Dispose();
            _searchDebounceCts = null;
            StopPoiMonitoring();
            StopSpeechAndHidePopup();
            base.OnDisappearing();
        }

        private async Task LoadCachedDataAsync()
        {
            try
            {
                var cached = await _stallService.LoadCachedStallsAsync();
                if (cached.Count == 0)
                {
                    return;
                }

                MainThread.BeginInvokeOnMainThread(() => DisplayStalls(cached));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"--- LOI TAI CACHE STALLS: {ex.Message}");
            }
        }

        private async Task LoadDataFromServer()
        {
            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                }

                Location? location = null;
                if (status == PermissionStatus.Granted)
                {
                    var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(5));
                    location = await Geolocation.Default.GetLocationAsync(request);
                }

                List<StallItem>? data;
                if (location != null)
                {
                    _lastKnownLocation = location;
                    data = await _stallService.GetNearbyStallsAsync(location.Latitude, location.Longitude);
                }
                else
                {
                    Debug.WriteLine("--- KHONG LAY DUOC GPS: Thu goi API voi toa do mac dinh hoac hien Mock Data");
                    _lastKnownLocation = new Location(10.7626, 106.7064);
                    data = await _stallService.GetNearbyStallsAsync(10.7626, 106.7064);
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (data != null && data.Count > 0)
                    {
                        DisplayStalls(data);
                        HideInitialLoading();
                        _ = PreloadNearbyAudioAsync(data);
                    }
                    else if (Stalls.Count == 0)
                    {
                        LoadMockData();
                        HideInitialLoading();
                    }
                    else
                    {
                        HideInitialLoading();
                    }
                });

                StartPoiMonitoring();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"--- LOI TAI MAINPAGE: {ex.Message}");
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (Stalls.Count == 0)
                    {
                        LoadMockData();
                    }

                    HideInitialLoading();
                });
            }
        }

        private void LoadMockData()
        {
            var mock = new StallItem(
                Id: 0,
                DistanceText: "50m",
                Name: "Oc Vinh Khanh (Demo)",
                Rating: "4.8",
                Reviews: "(120)",
                Cuisine: "Hai san",
                CategorySlug: "seafood",
                Distance: 0.05,
                Lat: 10.7626,
                Lng: 106.7064,
                PoiRadiusMeters: 30,
                HasOfflineAudio: false,
                Specialties: new List<string> { "Oc huong xao bo toi", "So diep nuong mo hanh", "Cang ghe rang muoi" },
                Translations: new Dictionary<string, string>
                {
                    ["vi"] = "Chao mung ban den voi thien duong oc Quan 4",
                    ["en"] = "Welcome to snail paradise D4",
                    ["ko"] = "D4 dalpaengi yori ui cheonguge osin geoseul hwan-yeonghabnida",
                    ["ja"] = "4 ku no kai ryori paradise e yokoso",
                    ["zh-CN"] = "Welcome to District 4 snail food paradise"
                },
                ImageUrl: ""
            );

            DisplayStalls(new List<StallItem> { mock });
            Debug.WriteLine("--- DA TAI DU LIEU MOCK THANH CONG");
        }

        private void DisplayStalls(IEnumerable<StallItem> stalls)
        {
            _nearbyStalls = stalls
                .Select(AttachOfflineFlag)
                .Select(LocalizeStall)
                .ToList();
            if (string.IsNullOrWhiteSpace(_searchText))
            {
                DisplayVisibleStalls(_nearbyStalls);
            }
            else
            {
                ApplySearchPreview();
            }
        }

        private void EnsureImageCacheSubscription()
        {
            if (_isSubscribedToImageCacheUpdates)
            {
                return;
            }

            _stallService.ImageCacheUpdated += OnImageCacheUpdated;
            _isSubscribedToImageCacheUpdates = true;
        }

        private void RemoveImageCacheSubscription()
        {
            if (!_isSubscribedToImageCacheUpdates)
            {
                return;
            }

            _stallService.ImageCacheUpdated -= OnImageCacheUpdated;
            _isSubscribedToImageCacheUpdates = false;
        }

        private void OnImageCacheUpdated(IReadOnlyList<StallItem> refreshedStalls)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var updatesById = refreshedStalls
                    .Where(item => item.Id > 0 && !string.IsNullOrWhiteSpace(item.ThumbnailUrl))
                    .ToDictionary(item => item.Id, item => item);

                if (updatesById.Count == 0)
                {
                    return;
                }

                for (var index = 0; index < Stalls.Count; index++)
                {
                    var current = Stalls[index];
                    if (!updatesById.TryGetValue(current.Id, out var refreshed))
                    {
                        continue;
                    }

                    if (current.ThumbnailUrl == refreshed.ThumbnailUrl && current.ImageUrl == refreshed.ImageUrl)
                    {
                        continue;
                    }

                    var updated = current with
                    {
                        ThumbnailUrl = refreshed.ThumbnailUrl,
                        ImageUrl = refreshed.ImageUrl
                    };
                    Stalls[index] = ApplyHighlight(updated, _searchText);
                }

                UpdateSourceListImages(_nearbyStalls, updatesById);
                UpdateSourceListImages(_remoteSearchStalls, updatesById);
            });
        }

        private static void UpdateSourceListImages(List<StallItem> source, IReadOnlyDictionary<int, StallItem> updatesById)
        {
            for (var index = 0; index < source.Count; index++)
            {
                var current = source[index];
                if (!updatesById.TryGetValue(current.Id, out var refreshed))
                {
                    continue;
                }

                source[index] = current with
                {
                    ThumbnailUrl = refreshed.ThumbnailUrl,
                    ImageUrl = refreshed.ImageUrl
                };
            }
        }

        private void ShowInitialLoading()
        {
            InitialLoadingOverlay.IsVisible = true;
        }

        private void HideInitialLoading()
        {
            InitialLoadingOverlay.IsVisible = false;
        }

        private StallItem AttachOfflineFlag(StallItem stall)
        {
            var languageCode = GetLanguageCode(_selectedLanguage);
            return stall with { HasOfflineAudio = _audioCacheService.HasCachedAudio(stall.Id, languageCode) };
        }

        private StallItem LocalizeStall(StallItem stall)
        {
            return stall.WithLocalizedCuisine(GetLanguageCode(_selectedLanguage));
        }

        private async void OnStallTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is StallItem stall)
            {
                await ShowScriptPopupAsync(stall);
            }
        }

        private async Task ShowScriptPopupAsync(StallItem stall)
        {
            var text = AppText.Get(_selectedLanguage);
            var languageCode = GetLanguageCode(_selectedLanguage);
            var content = stall.GetScript(languageCode);

            if (string.IsNullOrWhiteSpace(content))
            {
                var action = await DisplayActionSheet(
                    text.UnavailableContentMessage,
                    text.CancelText,
                    null,
                    AppText.Vietnamese,
                    AppText.English,
                    AppText.Japanese,
                    AppText.Korean);

                if (action is AppText.Vietnamese or AppText.English or AppText.Japanese or AppText.Korean)
                {
                    _selectedLanguage = action;
                    ApplyLanguage(_selectedLanguage);
                    await ShowScriptPopupAsync(stall);
                }

                return;
            }

            StopSpeech();
            StopAudioPlayback();

            ScriptPopupHeaderLabel.Text = text.ScriptDialogTitle;
            ScriptPopupTitleLabel.Text = stall.Name;
            ScriptPopupImage.Source = string.IsNullOrWhiteSpace(stall.ImageUrl) ? null : stall.ImageUrl;
            ScriptPopupCuisineLabel.Text = $"{text.PopupCuisineLabel}: {stall.Cuisine}";
            ScriptPopupDistanceLabel.Text = $"{text.PopupDistanceLabel}: {stall.DistanceText}";
            ScriptPopupRatingLabel.Text = $"{text.PopupRatingLabel}: {stall.Rating}";
            ScriptPopupReviewsLabel.Text = $"{text.PopupReviewsLabel}: {stall.Reviews}";
            var hoursText = stall.GetDisplayHours();
            ScriptPopupHoursLabel.Text = $"{text.PopupHoursLabel}: {(string.IsNullOrWhiteSpace(hoursText) ? "-" : hoursText)}";
            ScriptPopupSpecialtiesHeaderLabel.Text = text.PopupSpecialtiesLabel;
            PopulateSpecialties(stall.GetTopSpecialties());
            ScriptPopupContentLabel.Text = content;
            ScriptPopupCloseButton.Text = text.CloseText;
            _currentPopupStall = stall;
            UpdateAudioStatusUi(stall, text, languageCode);
            ScriptPopupOverlay.IsVisible = true;

            _speechCts = new CancellationTokenSource();
            var audioPath = await _audioCacheService.GetPlayableAudioPathAsync(stall.Id, languageCode);
            UpdateAudioStatusUi(stall, text, languageCode);
            RefreshOfflineFlag(stall.Id);

            if (!string.IsNullOrWhiteSpace(audioPath))
            {
                PlayCachedAudio(audioPath);
                await _stallService.LogListeningAsync(stall.Id, languageCode, 0);
            }
            else
            {
                if (AudioSettings.UseBackendAudioOnly)
                {
                    await DisplayAlert(text.ErrorTitle, text.AudioDownloadFailedMessage, "OK");
                    return;
                }

                _ = PlaySpeechAsync(stall, content, languageCode, text, _speechCts.Token);
            }
        }

        private async Task PlaySpeechAsync(
            StallItem stall,
            string content,
            string languageCode,
            LocalizedText text,
            CancellationToken cancellationToken)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
#if ANDROID
                if (AudioSettings.UseNativeAndroidTts)
                {
                    await SpeakWithAndroidTtsAsync(content, GetLocaleCode(_selectedLanguage), cancellationToken);
                }
                else
#endif
                {
                    var options = await BuildSpeechOptionsAsync(_selectedLanguage);
                    await MauiTextToSpeech.Default.SpeakAsync(content, options, cancellationToken);
                }
                stopwatch.Stop();

                if (!cancellationToken.IsCancellationRequested)
                {
                    await _stallService.LogListeningAsync(stall.Id, languageCode, (int)Math.Round(stopwatch.Elapsed.TotalSeconds));
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                await DisplayAlert(text.ErrorTitle, text.AudioErrorPrefix + ex.Message, "OK");
            }
        }

        private void ApplyLanguage(string selectedLanguage)
        {
            var text = AppText.Get(selectedLanguage);

            AwayLabel = text.AwayLabel;
            PageTitleLabel.Text = text.PageTitle;
            SearchBarControl.Placeholder = text.SearchHint;
            ExploreTabLabel.Text = text.ExploreTab;
            MapTabLabel.Text = text.MapTab;
            SavedTabLabel.Text = text.SavedTab;

            _nearbyStalls = _nearbyStalls.Select(LocalizeStall).ToList();
            _remoteSearchStalls = _remoteSearchStalls.Select(LocalizeStall).ToList();

            if (string.IsNullOrWhiteSpace(_searchText))
            {
                DisplayVisibleStalls(_nearbyStalls);
            }
            else if (_remoteSearchStalls.Count > 0)
            {
                DisplayVisibleStalls(_remoteSearchStalls);
            }
            else
            {
                ApplySearchPreview();
            }
        }

        private void OnClosePopupClicked(object sender, EventArgs e)
        {
            StopSpeechAndHidePopup();
        }

        private void OnPopupBackdropTapped(object sender, TappedEventArgs e)
        {
            StopSpeechAndHidePopup();
        }

        private void OnPopupCardTapped(object sender, TappedEventArgs e)
        {
        }

        private async void OnSavedTapped(object sender, TappedEventArgs e)
        {
            await Navigation.PushAsync(new DownloadedAudioPage(_audioCacheService));
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            _searchText = e.NewTextValue ?? string.Empty;
            var previousCts = _searchDebounceCts;
            previousCts?.Cancel();

            if (string.IsNullOrWhiteSpace(_searchText))
            {
                _lastRemoteSearchText = string.Empty;
                _remoteSearchStalls.Clear();
                DisplayVisibleStalls(_nearbyStalls);
                previousCts?.Dispose();
                _searchDebounceCts = null;
                return;
            }

            ApplySearchPreview();

            var queryTerms = SplitSearchTerms(_searchText);
            if (queryTerms.Count < 2)
            {
                previousCts?.Dispose();
                _searchDebounceCts = null;
                return;
            }

            var newCts = new CancellationTokenSource();
            _searchDebounceCts = newCts;
            previousCts?.Dispose();
            _ = DebouncedBackendSearchAsync(_searchText, newCts.Token);
        }

        private void ApplySearchPreview()
        {
            if (_nearbyStalls.Count == 0)
            {
                Stalls.ReplaceRange(Array.Empty<StallItem>());
                return;
            }

            var queryTerms = SplitSearchTerms(_searchText);
            var filtered = _nearbyStalls
                .Where(stall => MatchesSearch(stall, queryTerms))
                .ToList();

            DisplayVisibleStalls(filtered);
        }

        private async Task DebouncedBackendSearchAsync(string query, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(450, cancellationToken);

                if (string.Equals(query, _lastRemoteSearchText, StringComparison.Ordinal))
                {
                    return;
                }

                var searchResults = await _stallService.SearchStallsAsync(
                    query,
                    _lastKnownLocation?.Latitude,
                    _lastKnownLocation?.Longitude,
                    cancellationToken);

                if (cancellationToken.IsCancellationRequested || !string.Equals(query, _searchText, StringComparison.Ordinal))
                {
                    return;
                }

                _lastRemoteSearchText = query;
                _remoteSearchStalls = searchResults
                    .Select(AttachOfflineFlag)
                    .Select(LocalizeStall)
                    .ToList();

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (!string.IsNullOrWhiteSpace(_searchText))
                    {
                        DisplayVisibleStalls(_remoteSearchStalls);
                    }
                });
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"--- LOI TIM KIEM BACKEND: {ex.Message}");
            }
        }

        private void DisplayVisibleStalls(IEnumerable<StallItem> stalls)
        {
            var highlighted = stalls
                .Select(stall => ApplyHighlight(stall, _searchText))
                .ToList();
            Stalls.ReplaceRange(highlighted);
        }

        private static bool MatchesSearch(StallItem stall, IReadOnlyList<string> queryTerms)
        {
            if (queryTerms.Count == 0)
            {
                return true;
            }

            var normalizedTokens = BuildSearchCorpus(stall)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(NormalizeForSearch)
                .ToList();

            return queryTerms.All(term =>
                normalizedTokens.Any(token => token.Contains(term, StringComparison.Ordinal)));
        }

        private static IReadOnlyList<string> BuildSearchCorpus(StallItem stall)
        {
            var tokens = new List<string>
            {
                stall.Name,
                stall.Cuisine,
                stall.CategorySlug
            };

            if (stall.Specialties is { Count: > 0 })
            {
                tokens.AddRange(stall.Specialties);
            }

            return tokens;
        }

        private static IReadOnlyList<string> SplitSearchTerms(string query)
        {
            return NormalizeForSearch(query)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        private static StallItem ApplyHighlight(StallItem stall, string query)
        {
            return stall with
            {
                HighlightedName = BuildHighlightedText(stall.Name, query, "#1F2738", "#EF8F2A", true),
                HighlightedCuisine = BuildHighlightedText(stall.Cuisine, query, "#8B95A4", "#EF8F2A", false)
            };
        }

        private static FormattedString BuildHighlightedText(
            string? source,
            string query,
            string defaultColor,
            string highlightColor,
            bool boldHighlight)
        {
            var formatted = new FormattedString();
            var text = source ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(query))
            {
                formatted.Spans.Add(new Span { Text = text, TextColor = Color.FromArgb(defaultColor) });
                return formatted;
            }

            var match = FindHighlightRange(text, SplitSearchTerms(query));
            if (match is null)
            {
                formatted.Spans.Add(new Span { Text = text, TextColor = Color.FromArgb(defaultColor) });
                return formatted;
            }

            var (start, length) = match.Value;
            if (start > 0)
            {
                formatted.Spans.Add(new Span { Text = text[..start], TextColor = Color.FromArgb(defaultColor) });
            }

            formatted.Spans.Add(new Span
            {
                Text = text.Substring(start, length),
                TextColor = Color.FromArgb(highlightColor),
                FontAttributes = boldHighlight ? FontAttributes.Bold : FontAttributes.None
            });

            var end = start + length;
            if (end < text.Length)
            {
                formatted.Spans.Add(new Span { Text = text[end..], TextColor = Color.FromArgb(defaultColor) });
            }

            return formatted;
        }

        private static (int Start, int Length)? FindHighlightRange(string source, IReadOnlyList<string> queryTerms)
        {
            var matches = queryTerms
                .Select(term => FindHighlightRange(source, term))
                .Where(match => match is not null)
                .Select(match => match!.Value)
                .OrderBy(match => match.Start)
                .ThenByDescending(match => match.Length)
                .ToList();

            return matches.Count == 0 ? null : matches[0];
        }

        private static (int Start, int Length)? FindHighlightRange(string source, string normalizedTerm)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(normalizedTerm))
            {
                return null;
            }

            var normalizedBuilder = new StringBuilder();
            var indexMap = new List<int>();

            for (var i = 0; i < source.Length; i++)
            {
                foreach (var normalizedChar in NormalizeForSearch(source[i].ToString()))
                {
                    normalizedBuilder.Append(normalizedChar);
                    indexMap.Add(i);
                }
            }

            var normalizedSource = normalizedBuilder.ToString();
            var startIndex = normalizedSource.IndexOf(normalizedTerm, StringComparison.Ordinal);
            if (startIndex < 0)
            {
                return null;
            }

            var endIndex = startIndex + normalizedTerm.Length - 1;
            var originalStart = indexMap[startIndex];
            var originalEnd = indexMap[endIndex];
            return (originalStart, originalEnd - originalStart + 1);
        }

        private static string NormalizeForSearch(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);
            foreach (var ch in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (category != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(ch);
                }
            }

            return builder
                .ToString()
                .Replace('đ', 'd')
                .Replace('Đ', 'D')
                .ToLowerInvariant();
        }

        private void StopSpeechAndHidePopup()
        {
            StopSpeech();
            StopAudioPlayback();
            _currentPopupStall = null;
            ScriptPopupOverlay.IsVisible = false;
        }

        private void StartPoiMonitoring()
        {
            StopPoiMonitoring();
            _poiMonitorCts = new CancellationTokenSource();
            _ = MonitorPoiAsync(_poiMonitorCts.Token);
        }

        private void StopPoiMonitoring()
        {
            if (_poiMonitorCts is not null)
            {
                _poiMonitorCts.Cancel();
                _poiMonitorCts.Dispose();
                _poiMonitorCts = null;
            }

            _poiInsideStalls.Clear();
        }

        private async Task MonitorPoiAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(PoiMonitorInitialDelay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                    var stallSnapshot = await MainThread.InvokeOnMainThreadAsync(() => Stalls.ToList());
                    if (status == PermissionStatus.Granted && stallSnapshot.Count > 0)
                    {
                        var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(5));
                        var location = await Geolocation.Default.GetLocationAsync(request);
                        if (location != null)
                        {
                            await CheckPoiForLocationAsync(location, stallSnapshot, cancellationToken);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"--- LOI KIEM TRA POI: {ex.Message}");
                }

                try
                {
                    await Task.Delay(PoiMonitorInterval, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task CheckPoiForLocationAsync(
            Location userLocation,
            IReadOnlyCollection<StallItem> stalls,
            CancellationToken cancellationToken = default)
        {
            var nearbyPoiStalls = stalls
                .Where(stall => stall.Id > 0 && stall.PoiRadiusMeters > 0 && stall.Lat != 0 && stall.Lng != 0)
                .Select(stall => new
                {
                    Stall = stall,
                    DistanceMeters = Location.CalculateDistance(
                        userLocation.Latitude,
                        userLocation.Longitude,
                        stall.Lat,
                        stall.Lng,
                        DistanceUnits.Kilometers) * 1000
                })
                .Where(item => item.DistanceMeters <= item.Stall.PoiRadiusMeters)
                .OrderBy(item => item.DistanceMeters)
                .ToList();

            var currentInsideIds = nearbyPoiStalls.Select(item => item.Stall.Id).ToHashSet();
            var enteredPoi = nearbyPoiStalls.FirstOrDefault(item => !_poiInsideStalls.Contains(item.Stall.Id));

            _poiInsideStalls.Clear();
            foreach (var stallId in currentInsideIds)
            {
                _poiInsideStalls.Add(stallId);
            }

            if (enteredPoi is null || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (ScriptPopupOverlay.IsVisible || _currentPopupStall is not null)
            {
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(() => ShowScriptPopupAsync(enteredPoi.Stall));
        }

        private async Task PreloadNearbyAudioAsync(IEnumerable<StallItem> stalls)
        {
            var languageCode = GetLanguageCode(_selectedLanguage);
            var cachedIds = await _audioCacheService.PreloadTopStallsAsync(stalls, languageCode);
            if (cachedIds.Count == 0)
            {
                return;
            }

            MainThread.BeginInvokeOnMainThread(() => RefreshOfflineFlags(cachedIds));
        }

        private void RefreshOfflineFlag(int stallId)
        {
            RefreshOfflineFlags(new[] { stallId });
        }

        private void RefreshOfflineFlags(IEnumerable<int> stallIds)
        {
            var targetIds = stallIds.Distinct().ToHashSet();
            if (targetIds.Count == 0)
            {
                return;
            }

            for (var index = 0; index < Stalls.Count; index++)
            {
                var current = Stalls[index];
                if (!targetIds.Contains(current.Id))
                {
                    continue;
                }

                var updated = AttachOfflineFlag(current);
                if (!EqualityComparer<StallItem>.Default.Equals(current, updated))
                {
                    Stalls[index] = updated;
                }
            }
        }

        private void PopulateSpecialties(IReadOnlyList<string> specialties)
        {
            ScriptPopupSpecialtiesContainer.Children.Clear();

            foreach (var specialty in specialties.Take(3))
            {
                ScriptPopupSpecialtiesContainer.Children.Add(new Border
                {
                    BackgroundColor = Color.FromArgb("#F6EFE7"),
                    StrokeThickness = 0,
                    Padding = new Thickness(10, 8),
                    StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(14) },
                    Content = new Label
                    {
                        Text = "• " + specialty,
                        FontSize = 14,
                        TextColor = Color.FromArgb("#425066")
                    }
                });
            }
        }

        private void StopSpeech()
        {
            if (_speechCts is not null)
            {
                _speechCts.Cancel();
                _speechCts.Dispose();
                _speechCts = null;
            }

#if ANDROID
            if (_androidTts is not null)
            {
                _androidTts.Stop();
                _androidTtsSpeakTcs?.TrySetCanceled();
                _androidTtsSpeakTcs = null;
            }
#endif
        }

        private void PlayCachedAudio(string audioPath)
        {
            StopAudioPlayback();
#if ANDROID
            _androidMediaPlayer = new MediaPlayer();
            _androidMediaPlayer.SetDataSource(audioPath);
            _androidMediaPlayer.Prepared += OnAndroidMediaPrepared;
            _androidMediaPlayer.Completion += OnAndroidMediaCompleted;
            _androidMediaPlayer.PrepareAsync();
#endif
        }

        private void StopAudioPlayback()
        {
#if ANDROID
            if (_androidMediaPlayer is null)
            {
                return;
            }

            _androidMediaPlayer.Prepared -= OnAndroidMediaPrepared;
            _androidMediaPlayer.Completion -= OnAndroidMediaCompleted;

            try
            {
                if (_androidMediaPlayer.IsPlaying)
                {
                    _androidMediaPlayer.Stop();
                }
            }
            catch
            {
                // Ignore stop failures during teardown.
            }

            _androidMediaPlayer.Release();
            _androidMediaPlayer.Dispose();
            _androidMediaPlayer = null;
#endif
        }

#if ANDROID
        private void OnAndroidMediaPrepared(object? sender, EventArgs e)
        {
            _androidMediaPlayer?.Start();
        }

        private void OnAndroidMediaCompleted(object? sender, EventArgs e)
        {
            StopAudioPlayback();
        }
#endif

        private void UpdateAudioStatusUi(StallItem stall, LocalizedText text, string languageCode)
        {
            var hasCachedAudio = _audioCacheService.HasCachedAudio(stall.Id, languageCode);
            ScriptPopupAudioStatusLabel.Text = hasCachedAudio ? text.AudioReadyLabel : text.AudioNotReadyLabel;
            ScriptPopupAudioStatusLabel.TextColor = hasCachedAudio ? Color.FromArgb("#2E8F52") : Color.FromArgb("#607086");
            ScriptPopupDownloadButton.Text = text.DownloadAudioButton;
            ScriptPopupDownloadButton.IsVisible = !hasCachedAudio && stall.Id > 0;
            ScriptPopupDownloadButton.IsEnabled = stall.Id > 0;
        }

        private async void OnDownloadAudioClicked(object sender, EventArgs e)
        {
            if (_currentPopupStall is null)
            {
                return;
            }

            var text = AppText.Get(_selectedLanguage);
            var languageCode = GetLanguageCode(_selectedLanguage);

            ScriptPopupDownloadButton.IsEnabled = false;
            ScriptPopupDownloadButton.Text = text.DownloadingAudioButton;

            var success = await _audioCacheService.PreloadAudioAsync(_currentPopupStall.Id, languageCode);
            UpdateAudioStatusUi(_currentPopupStall, text, languageCode);
            RefreshOfflineFlag(_currentPopupStall.Id);

            if (success)
            {
                await DisplayAlert(text.ScriptDialogTitle, text.AudioDownloadedMessage, "OK");
            }
            else
            {
                ScriptPopupDownloadButton.IsEnabled = true;
                await DisplayAlert(text.ErrorTitle, text.AudioDownloadFailedMessage, "OK");
            }
        }

        private static string GetLanguageCode(string selectedLanguage) => selectedLanguage switch
        {
            AppText.English => "en",
            AppText.Chinese => "zh-CN",
            AppText.Japanese => "ja",
            AppText.Korean => "ko",
            _ => "vi"
        };

        private static string GetLocaleCode(string selectedLanguage) => selectedLanguage switch
        {
            AppText.English => "en-US",
            AppText.Chinese => "zh-CN",
            AppText.Japanese => "ja-JP",
            AppText.Korean => "ko-KR",
            _ => "vi-VN"
        };

        private static async Task<SpeechOptions> BuildSpeechOptionsAsync(string selectedLanguage)
        {
            var localeCode = GetLocaleCode(selectedLanguage);
            var locales = await MauiTextToSpeech.Default.GetLocalesAsync();
            var locale = locales.FirstOrDefault(l =>
                string.Equals(l.Language, localeCode, StringComparison.OrdinalIgnoreCase))
                ?? locales.FirstOrDefault(l =>
                    l.Language.StartsWith(localeCode.Split('-')[0], StringComparison.OrdinalIgnoreCase));

            return new SpeechOptions
            {
                Locale = locale,
                Pitch = AudioSettings.FallbackTtsPitch,
                Volume = AudioSettings.FallbackTtsVolume
            };
        }

#if ANDROID
        private async Task SpeakWithAndroidTtsAsync(string content, string localeCode, CancellationToken cancellationToken)
        {
            var tts = await EnsureAndroidTtsAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var locale = AndroidLocale.ForLanguageTag(localeCode);
            var languageStatus = tts.SetLanguage(locale);
            if (languageStatus == AndroidTtsLanguageAvailableResult.MissingData
                || languageStatus == AndroidTtsLanguageAvailableResult.NotSupported)
            {
                tts.SetLanguage(new AndroidLocale(localeCode.Split('-')[0]));
            }

            tts.SetPitch(AudioSettings.AndroidTtsPitch);
            tts.SetSpeechRate(AudioSettings.AndroidTtsSpeechRate);

            var utteranceId = Guid.NewGuid().ToString("N");
            var speakTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _androidTtsSpeakTcs = speakTcs;
            _androidTtsUtteranceId = utteranceId;

            using var registration = cancellationToken.Register(() =>
            {
                tts.Stop();
                speakTcs.TrySetCanceled(cancellationToken);
            });

            var result = tts.Speak(content, AndroidTtsQueueMode.Flush, null, utteranceId);
            if (result == AndroidTtsOperationResult.Error)
            {
                _androidTtsSpeakTcs = null;
                _androidTtsUtteranceId = null;
                throw new InvalidOperationException("Android TTS không thể bắt đầu phát.");
            }

            await speakTcs.Task;
        }

        private async Task<AndroidTextToSpeech> EnsureAndroidTtsAsync(CancellationToken cancellationToken)
        {
            if (_androidTts is not null)
            {
                return _androidTts;
            }

            _androidTtsInitTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _androidTts = new AndroidTextToSpeech(Android.App.Application.Context, new AndroidTtsInitListener(this));
            _androidTts.SetOnUtteranceProgressListener(new AndroidTtsProgressListener(this));

            using var registration = cancellationToken.Register(() => _androidTtsInitTcs?.TrySetCanceled(cancellationToken));
            await _androidTtsInitTcs.Task;
            return _androidTts;
        }

        private sealed class AndroidTtsInitListener : Java.Lang.Object, AndroidTextToSpeech.IOnInitListener
        {
            private readonly MainPage _page;

            public AndroidTtsInitListener(MainPage page)
            {
                _page = page;
            }

            public void OnInit(AndroidTtsOperationResult status)
            {
                if (status == AndroidTtsOperationResult.Success)
                {
                    _page._androidTtsInitTcs?.TrySetResult(true);
                    return;
                }

                _page._androidTtsInitTcs?.TrySetException(new InvalidOperationException("Android TTS khởi tạo thất bại."));
            }
        }

        private sealed class AndroidTtsProgressListener : AndroidUtteranceProgressListener
        {
            private readonly MainPage _page;

            public AndroidTtsProgressListener(MainPage page)
            {
                _page = page;
            }

            public override void OnStart(string? utteranceId)
            {
            }

            public override void OnDone(string? utteranceId)
            {
                if (utteranceId == _page._androidTtsUtteranceId)
                {
                    _page._androidTtsSpeakTcs?.TrySetResult(true);
                }
            }

            [Obsolete]
            public override void OnError(string? utteranceId)
            {
                if (utteranceId == _page._androidTtsUtteranceId)
                {
                    _page._androidTtsSpeakTcs?.TrySetException(new InvalidOperationException("Android TTS phát lỗi."));
                }
            }

        }
#endif

        private async Task NavigateBackAsync()
        {
            if (Navigation.NavigationStack.Count > 1)
            {
                await Navigation.PopAsync();
            }
            else
            {
                var window = Application.Current?.Windows.FirstOrDefault();
                if (window is not null)
                {
                    window.Page = new NavigationPage(new LanguageSelectionPage(_stallService, _audioCacheService));
                }
            }
        }
    }
}
