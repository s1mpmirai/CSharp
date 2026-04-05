using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using CommunityToolkit.Maui.Views;
using FoodStreetAudioGuide.Models;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Storage;
using MauiTextToSpeech = Microsoft.Maui.Media.TextToSpeech;
#if ANDROID
using AndroidTtsLanguageAvailableResult = Android.Speech.Tts.LanguageAvailableResult;
using Android.Media;
using Android.Content;
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
        private string _offlineBadgeLabel = "OFFLINE";
        private CancellationTokenSource? _speechCts;
        private CancellationTokenSource? _poiMonitorCts;
        private readonly PoiGeofenceEngine _poiGeofenceEngine = new();
        private readonly GpsMonitoringPolicy _gpsMonitoringPolicy = new();
        private bool _hasLoadedInitially;
        private static readonly TimeSpan PoiMonitorInitialDelay = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan PoiMonitorInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan PoiMonitorCachedLocationWindow = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan PoiMonitorActiveLocationTimeout = TimeSpan.FromSeconds(4);
        private const double PoiMonitorAcceptedAccuracyMeters = 20;
        private static readonly TimeSpan PreciseLocationTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan PreciseLocationSampleDelay = TimeSpan.FromMilliseconds(250);
        private const int PreciseLocationSampleCount = 2;
        private const double DesiredPoiAccuracyMeters = 12;
        private static readonly TimeSpan FreshLastKnownLocationWindow = TimeSpan.FromSeconds(10);
        private const double AcceptableLastKnownAccuracyMeters = 18;
        private static readonly TimeSpan FastRefreshLocationWindow = TimeSpan.FromMinutes(2);
        private const double FastRefreshAcceptedAccuracyMeters = 50;
        private static readonly TimeSpan SoftRefreshLocationWindow = TimeSpan.FromMinutes(10);
        private const double SoftRefreshAcceptedAccuracyMeters = 150;
        private bool _isSubscribedToImageCacheUpdates;
        private List<StallItem> _nearbyStalls = new();
        private List<StallItem> _remoteSearchStalls = new();
        private string _searchText = string.Empty;
        private string _lastRemoteSearchText = string.Empty;
        private CancellationTokenSource? _searchDebounceCts;
        private Location? _lastKnownLocation;
        private Location? _lastNearbyFetchLocation;
        private bool _isScriptExpanded;
        private bool _isOpeningMap;
        private bool _isOpeningQr;
        private bool _isSubscribedToConnectivityChanges;
        private List<StallItem> _visibleStallSource = new();
        private int _currentPoiPage = 1;
        private string _localizedSnapshotLanguage = string.Empty;
        private int _localizedSnapshotCount = -1;
        private List<StallItem> _localizedNearbySnapshot = new();
        private string? _lastKnownSyncVersion;
        private DateTime _lastSyncCheckUtc = DateTime.MinValue;
        private bool _isRefreshingFromServer;
        private bool _isSubmittingRating;
        private CancellationTokenSource? _backgroundSyncCts;
        private CancellationTokenSource? _deferredAudioPreloadCts;
        private readonly HashSet<int> _ratedStallIds = new();
        private static readonly TimeSpan BackgroundSyncInterval = TimeSpan.FromSeconds(8);
        private static readonly TimeSpan DeferredAudioPreloadDelay = TimeSpan.FromMilliseconds(900);
        private const int PoiPageSize = 5;
        private const string RatedStallIdsPreferenceKey = "rated_stall_ids";
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

        public string OfflineBadgeLabel
        {
            get => _offlineBadgeLabel;
            set
            {
                if (_offlineBadgeLabel == value)
                {
                    return;
                }

                _offlineBadgeLabel = value;
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
            _poiGeofenceEngine.MaxAcceptedAccuracyMeters = 30;
            _poiGeofenceEngine.MinimumEntryMarginMeters = 2;
            _poiGeofenceEngine.ExitMarginMeters = 8;
            _poiGeofenceEngine.RequiredConsecutiveSamples = 2;
            _gpsMonitoringPolicy.MeaningfulMovementMeters = 2.5;
            _gpsMonitoringPolicy.NearbyRefreshMovementMeters = 3.0;
            LoadRatedStallIds();

            ApplyLanguage(_selectedLanguage);
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            App.AppMovedToBackground -= HandleAppMovedToBackground;
            App.AppMovedToBackground += HandleAppMovedToBackground;
            EnsureImageCacheSubscription();
            EnsureConnectivitySubscription();
            StartBackgroundSync();
            if (_hasLoadedInitially)
            {
                _ = RefreshFromServerIfChangedAsync();
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
            App.AppMovedToBackground -= HandleAppMovedToBackground;
            RemoveImageCacheSubscription();
            RemoveConnectivitySubscription();
            _searchDebounceCts?.Cancel();
            _searchDebounceCts?.Dispose();
            _searchDebounceCts = null;
            StopBackgroundSync();
            StopPoiMonitoring();
            _deferredAudioPreloadCts?.Cancel();
            _deferredAudioPreloadCts?.Dispose();
            _deferredAudioPreloadCts = null;
            StopSpeechAndHidePopup();
            base.OnDisappearing();
        }

        private void HandleAppMovedToBackground()
        {
            MainThread.BeginInvokeOnMainThread(StopSpeechAndHidePopup);
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

        private async Task LoadDataFromServer(bool preferResponsiveLocation = false)
        {
            if (_isRefreshingFromServer)
            {
                return;
            }

            _isRefreshingFromServer = true;
            MainThread.BeginInvokeOnMainThread(UpdateRefreshButtonState);
            await Task.Yield();
            try
            {
                if (!CanAttemptBackendRequest())
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (Stalls.Count == 0)
                        {
                            LoadMockData();
                        }

                        HideInitialLoading();
                    });

                    StartPoiMonitoring();
                    return;
                }

                var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                }

                var syncVersionTask = _stallService.GetSyncVersionAsync();
                Location? location = null;
                if (status == PermissionStatus.Granted)
                {
                    location = preferResponsiveLocation
                        ? await GetResponsiveLocationAsync()
                        : await GetBestAvailableLocationAsync();
#if ANDROID
                    EnsureAndroidBackgroundTracking();
#endif
                }

                var requestLocation = location;
                if (requestLocation is null && preferResponsiveLocation && IsAcceptableLocation(_lastKnownLocation, FastRefreshLocationWindow, FastRefreshAcceptedAccuracyMeters))
                {
                    requestLocation = _lastKnownLocation;
                }

                List<StallItem>? data;
                if (requestLocation != null)
                {
                    _lastKnownLocation = requestLocation;
                    _lastNearbyFetchLocation = requestLocation;
                    data = await _stallService.GetNearbyStallsAsync(requestLocation.Latitude, requestLocation.Longitude);
                }
                else
                {
                    Debug.WriteLine("--- KHONG LAY DUOC GPS: Thu goi API voi toa do mac dinh hoac hien Mock Data");
                    requestLocation = _lastKnownLocation ?? new Location(10.7626, 106.7064);
                    _lastKnownLocation = requestLocation;
                    _lastNearbyFetchLocation = requestLocation;
                    data = await _stallService.GetNearbyStallsAsync(requestLocation.Latitude, requestLocation.Longitude);
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (data != null && data.Count > 0)
                    {
                        DisplayStalls(data);
                        HideInitialLoading();
                        ScheduleNearbyAudioPreload(data);
                    }
                    else if (Stalls.Count == 0)
                    {
                        if (!CanAttemptBackendRequest())
                        {
                            LoadMockData();
                        }

                        HideInitialLoading();
                    }
                    else
                    {
                        HideInitialLoading();
                    }
                });

                var latestSyncVersion = await syncVersionTask;
                if (!string.IsNullOrWhiteSpace(latestSyncVersion))
                {
                    _lastKnownSyncVersion = latestSyncVersion;
                }
                StartPoiMonitoring();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"--- LOI TAI MAINPAGE: {ex.Message}");
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (Stalls.Count == 0)
                    {
                        if (!CanAttemptBackendRequest())
                        {
                            LoadMockData();
                        }
                    }

                    HideInitialLoading();
                });
            }
            finally
            {
                _isRefreshingFromServer = false;
                MainThread.BeginInvokeOnMainThread(UpdateRefreshButtonState);
            }
        }

        private void LoadMockData()
        {
            var mock = new List<StallItem>
            {
                new(
                    Id: 1,
                    DistanceText: "40m",
                    Name: "Ốc Vĩnh Khánh (Offline)",
                    Rating: "4.8",
                    Reviews: "(120)",
                    Cuisine: "Hải sản",
                    CategorySlug: "seafood",
                    Distance: 0.04,
                    Lat: 10.7626,
                    Lng: 106.7064,
                    PoiRadiusMeters: 30,
                    HasOfflineAudio: false,
                    Specialties: new List<string> { "Ốc hương xào bơ tỏi", "Sò điệp nướng mỡ hành", "Càng ghẹ rang muối" },
                    SpecialtyTranslations: new Dictionary<string, List<string>>
                    {
                        ["vi"] = new() { "Ốc hương xào bơ tỏi", "Sò điệp nướng mỡ hành", "Càng ghẹ rang muối" },
                        ["en"] = new() { "Wok-fried sea snails with garlic butter", "Grilled scallops with scallion oil", "Salt-roasted crab claws" },
                        ["zh-CN"] = new() { "蒜香黄油炒香螺", "葱油烤扇贝", "椒盐炒蟹钳" },
                        ["ja"] = new() { "ニンニクバター炒めの巻き貝", "ねぎ油をのせた焼きホタテ", "塩炒めのカニの爪" },
                        ["ko"] = new() { "갈릭버터 소스로 볶은 소라", "쪽파기름을 올린 구운 가리비", "소금볶음 꽃게 집게" }
                    },
                    Translations: new Dictionary<string, string>
                    {
                        ["vi"] = "Bạn đang ở gần khu ốc Vĩnh Khánh nổi tiếng của Quận 4.",
                        ["en"] = "You are near the famous Vinh Khanh snail street in District 4.",
                        ["zh-CN"] = "您现在位于第四郡著名的永庆螺蛳美食街附近。",
                        ["ja"] = "ここは4区で有名なヴィンカイン通りの貝料理エリアの近くです。",
                        ["ko"] = "지금 여러분은 4군의 유명한 빈카인 달팽이 요리 거리 근처에 있습니다."
                    },
                    ImageUrl: ""),
                new(
                    Id: 2,
                    DistanceText: "85m",
                    Name: "Phá lấu Cô Thảo (Offline)",
                    Rating: "4.6",
                    Reviews: "(88)",
                    Cuisine: "Món nước",
                    CategorySlug: "noodle",
                    Distance: 0.08,
                    Lat: 10.7630,
                    Lng: 106.7060,
                    PoiRadiusMeters: 25,
                    HasOfflineAudio: false,
                    Specialties: new List<string> { "Phá lấu bò", "Bánh mì phá lấu", "Mì phá lấu" },
                    SpecialtyTranslations: new Dictionary<string, List<string>>
                    {
                        ["vi"] = new() { "Phá lấu bò", "Bánh mì phá lấu", "Mì phá lấu" },
                        ["en"] = new() { "Braised beef offal stew", "Bread with braised beef offal", "Noodles with braised beef offal" },
                        ["zh-CN"] = new() { "越式香料炖牛杂", "牛杂炖汁法棍", "牛杂炖汁面" },
                        ["ja"] = new() { "牛モツのスパイス煮込み", "牛モツ煮込みのバインミー", "牛モツ煮込み麺" },
                        ["ko"] = new() { "향신료로 졸인 소 내장 요리", "파러우를 넣은 바게트 샌드", "파러우를 곁들인 국수" }
                    },
                    Translations: new Dictionary<string, string>
                    {
                        ["vi"] = "Đây là một điểm phá lấu bình dân phù hợp để demo offline.",
                        ["en"] = "This is an offline demo stall for pha lau street food.",
                        ["zh-CN"] = "这是一个适合离线演示的平价越南破烂牛杂摊位。",
                        ["ja"] = "これはオフラインデモ用に用意した、手頃な価格のファーラウ屋台です。",
                        ["ko"] = "이곳은 오프라인 데모용으로 준비한 부담 없는 파러우 길거리 음식 가판대입니다."
                    },
                    ImageUrl: ""),
                new(
                    Id: 3,
                    DistanceText: "120m",
                    Name: "Chè Cô Lan (Offline)",
                    Rating: "4.7",
                    Reviews: "(64)",
                    Cuisine: "Tráng miệng",
                    CategorySlug: "dessert",
                    Distance: 0.12,
                    Lat: 10.7622,
                    Lng: 106.7058,
                    PoiRadiusMeters: 20,
                    HasOfflineAudio: false,
                    Specialties: new List<string> { "Chè khúc bạch", "Chè đậu đỏ", "Sâm bổ lượng" },
                    SpecialtyTranslations: new Dictionary<string, List<string>>
                    {
                        ["vi"] = new() { "Chè khúc bạch", "Chè đậu đỏ", "Sâm bổ lượng" },
                        ["en"] = new() { "Almond panna cotta dessert soup", "Sweet red bean dessert", "Herbal sweet soup" },
                        ["zh-CN"] = new() { "杏仁奶冻甜汤", "红豆甜品", "清补凉甜汤" },
                        ["ja"] = new() { "杏仁ミルクプリンのチェー", "あずきチェー", "漢方シロップのデザートスープ" },
                        ["ko"] = new() { "아몬드 판나코타 디저트", "팥 디저트", "한방 디저트 탕" }
                    },
                    Translations: new Dictionary<string, string>
                    {
                        ["vi"] = "Quán chè này được đưa vào bộ dữ liệu offline để app vẫn chạy được khi mất mạng.",
                        ["en"] = "This dessert stall is included in the offline starter dataset.",
                        ["zh-CN"] = "这个甜品摊位已加入离线初始数据包，因此应用在断网时仍可运行。",
                        ["ja"] = "このデザート屋台は、オフラインでもアプリが動作するよう初期データセットに含まれています。",
                        ["ko"] = "이 디저트 가판대는 오프라인 상태에서도 앱이 동작하도록 기본 오프라인 데이터에 포함되어 있습니다."
                    },
                    ImageUrl: "")
            };

            DisplayStalls(mock);
            Debug.WriteLine("--- DA TAI DU LIEU MOCK THANH CONG");
        }

        private void DisplayStalls(IEnumerable<StallItem> stalls)
        {
            _nearbyStalls = stalls
                .Select(AttachOfflineFlag)
                .Select(LocalizeStall)
                .Select(UpdateDistanceFromCurrentLocation)
                .OrderBy(stall => stall.Distance <= 0 ? double.MaxValue : stall.Distance)
                .ThenBy(stall => stall.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            InvalidateLocalizedSnapshots();
            if (string.IsNullOrWhiteSpace(_searchText))
            {
                DisplayVisibleStalls(_nearbyStalls, resetPage: true);
            }
            else
            {
                ApplySearchPreview();
            }

            RefreshCurrentPopupIfNeeded();
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

                var visibleChanged = false;
                var updatedVisible = new List<StallItem>(Stalls.Count);
                for (var index = 0; index < Stalls.Count; index++)
                {
                    var current = Stalls[index];
                    if (!updatesById.TryGetValue(current.Id, out var refreshed))
                    {
                        updatedVisible.Add(current);
                        continue;
                    }

                    if (current.ThumbnailUrl == refreshed.ThumbnailUrl && current.ImageUrl == refreshed.ImageUrl)
                    {
                        updatedVisible.Add(current);
                        continue;
                    }

                    var updated = current with
                    {
                        ThumbnailUrl = refreshed.ThumbnailUrl,
                        ImageUrl = refreshed.ImageUrl
                    };
                    updatedVisible.Add(ApplyHighlight(updated, _searchText));
                    visibleChanged = true;
                }

                if (visibleChanged)
                {
                    Stalls.ReplaceRange(updatedVisible);
                }

                UpdateSourceListImages(_nearbyStalls, updatesById);
                UpdateSourceListImages(_remoteSearchStalls, updatesById);
                RefreshCurrentPopupIfNeeded();
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
            return stall with { HasOfflineAudio = _audioCacheService.HasCachedAudio(stall, languageCode) };
        }

        private StallItem LocalizeStall(StallItem stall)
        {
            return stall.WithLocalizedCuisine(GetLanguageCode(_selectedLanguage));
        }

        private async void OnStallTapped(object sender, TappedEventArgs e)
        {
            DismissSearchKeyboard();
            if (e.Parameter is StallItem stall)
            {
                await ShowScriptPopupAsync(stall);
            }
        }

        private async Task ShowScriptPopupAsync(StallItem stall)
        {
            DismissSearchKeyboard();
            var text = AppText.Get(_selectedLanguage);
            var languageCode = GetLanguageCode(_selectedLanguage);
            stall = GetLatestStallSnapshot(stall) ?? stall;
            var content = stall.GetScript(languageCode);

            if (!string.IsNullOrWhiteSpace(content))
            {
                StopSpeech();
                StopAudioPlayback();
                RenderScriptPopup(stall, text, languageCode);
            }
            else
            {
                StopSpeech();
                StopAudioPlayback();
            }

            StallItem? refreshedStall = null;
            if (CanAttemptBackendRequest() && stall.Id > 0)
            {
                refreshedStall = await _stallService.GetStallDetailAsync(
                    stall.Id,
                    _lastKnownLocation?.Latitude,
                    _lastKnownLocation?.Longitude);
                if (refreshedStall is not null)
                {
                    refreshedStall = LocalizeStall(AttachOfflineFlag(PreserveDistanceIfMissing(stall, refreshedStall)));
                    UpdateNearbySnapshot(refreshedStall);
                    stall = refreshedStall;
                    content = stall.GetScript(languageCode);
                    RenderScriptPopup(stall, text, languageCode);
                }
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                var action = await DisplayActionSheet(
                    text.UnavailableContentMessage,
                    text.CancelText,
                    null,
                    AppText.Vietnamese,
                    AppText.English,
                    AppText.Chinese,
                    AppText.Japanese,
                    AppText.Korean);

                if (action is AppText.Vietnamese or AppText.English or AppText.Chinese or AppText.Japanese or AppText.Korean)
                {
                    _selectedLanguage = action;
                    ApplyLanguage(_selectedLanguage);
                    await ShowScriptPopupAsync(stall);
                }

                return;
            }

            _speechCts = new CancellationTokenSource();
            var audioPath = await _audioCacheService.GetPlayableAudioPathAsync(stall, languageCode);
            UpdateAudioStatusUi(stall, text, languageCode);
            RefreshOfflineFlag(stall.Id);

            if (!string.IsNullOrWhiteSpace(audioPath))
            {
                PlayCachedAudio(audioPath);
                await _stallService.LogListeningAsync(stall.Id, languageCode, 0, _lastKnownLocation);
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
                    await _stallService.LogListeningAsync(stall.Id, languageCode, (int)Math.Round(stopwatch.Elapsed.TotalSeconds), _lastKnownLocation);
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
            OfflineBadgeLabel = text.AudioBadgeText;
            PageTitleLabel.Text = text.PageTitle;
            SearchBarControl.Placeholder = text.SearchHint;
            ExploreTabLabel.Text = text.ExploreTab;
            SavedTabLabel.Text = text.SavedTab;
            InitialLoadingLabel.Text = text.LoadingNearbyStallsText;
            UpdateRefreshButtonState();

            _nearbyStalls = _nearbyStalls.Select(LocalizeStall).ToList();
            _remoteSearchStalls = _remoteSearchStalls.Select(LocalizeStall).ToList();
            InvalidateLocalizedSnapshots();

            if (string.IsNullOrWhiteSpace(_searchText))
            {
                DisplayVisibleStalls(_nearbyStalls, resetPage: true);
            }
            else if (_remoteSearchStalls.Count > 0)
            {
                DisplayVisibleStalls(_remoteSearchStalls, resetPage: true);
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

        private async void OnNavigateToStallClicked(object sender, EventArgs e)
        {
            DismissSearchKeyboard();
            if (_currentPopupStall is null)
            {
                return;
            }

            var selectedStall = _currentPopupStall;
            StopSpeechAndHidePopup();
            await OpenMapForStallAsync(selectedStall);
        }

        private void OnToggleScriptExpandedClicked(object sender, EventArgs e)
        {
            _isScriptExpanded = !_isScriptExpanded;
            UpdateScriptExpansionUi();
        }

        private void OnPopupBackdropTapped(object sender, TappedEventArgs e)
        {
            DismissSearchKeyboard();
            StopSpeechAndHidePopup();
        }

        private void OnPopupCardTapped(object sender, TappedEventArgs e)
        {
            DismissSearchKeyboard();
        }

        private async void OnSavedTapped(object sender, TappedEventArgs e)
        {
            await Navigation.PushAsync(new DownloadedAudioPage(_audioCacheService));
        }

        private async void OnQrScanTapped(object sender, TappedEventArgs e)
        {
            if (_isOpeningQr)
            {
                return;
            }

            _isOpeningQr = true;
            try
            {
                var text = AppText.Get(_selectedLanguage);
                var cameraStatus = await Permissions.CheckStatusAsync<Permissions.Camera>();
                if (cameraStatus != PermissionStatus.Granted)
                {
                    cameraStatus = await Permissions.RequestAsync<Permissions.Camera>();
                }

                if (cameraStatus != PermissionStatus.Granted)
                {
                    await DisplayAlert(text.QrTitle, text.QrCameraPermissionMessage, "OK");
                    return;
                }

                var scannerPage = new QrScannerModalPage(text);
                await Navigation.PushModalAsync(scannerPage);
                var rawValue = await scannerPage.WaitForResultAsync();
                if (string.IsNullOrWhiteSpace(rawValue))
                {
                    return;
                }

                await HandleQrScanResultAsync(rawValue);
            }
            finally
            {
                _isOpeningQr = false;
            }
        }

        private async void OnMapOpenTapped(object sender, TappedEventArgs e)
        {
            await OpenMapForStallAsync();
        }

        private async void OnRefreshDataClicked(object sender, EventArgs e)
        {
            if (_isRefreshingFromServer)
            {
                return;
            }

            _lastSyncCheckUtc = DateTime.MinValue;
            _lastKnownSyncVersion = null;
            if (Stalls.Count == 0)
            {
                ShowInitialLoading();
            }

            await Task.Yield();
            await LoadDataFromServer(preferResponsiveLocation: true);
        }

        private async Task OpenMapForStallAsync(StallItem? preferredStall = null)
        {
            if (_isOpeningMap)
            {
                return;
            }

            _isOpeningMap = true;
            try
            {
                var text = AppText.Get(_selectedLanguage);
                var mapStalls = _nearbyStalls.Count > 0
                    ? _nearbyStalls
                    : await _stallService.GetMapStallsAsync(_lastKnownLocation?.Latitude, _lastKnownLocation?.Longitude);

                if (mapStalls.Count == 0)
                {
                    mapStalls = _nearbyStalls;
                }

                if (mapStalls.Count == 0)
                {
                    await DisplayAlert(text.MapTitle, text.MapNoDataMessage, "OK");
                    return;
                }

                var localizedMapStalls = PrepareMapStalls(mapStalls);
                var preferredStallId = preferredStall?.Id;

                var mapPage = new PoiMapPage(localizedMapStalls, _lastKnownLocation, text, async stall =>
                {
                    await MainThread.InvokeOnMainThreadAsync(() => ShowScriptPopupAsync(stall));
                }, preferredStallId);

                await Navigation.PushModalAsync(mapPage);
            }
            finally
            {
                _isOpeningMap = false;
            }
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
                DisplayVisibleStalls(_nearbyStalls, resetPage: true);
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
                _visibleStallSource = new List<StallItem>();
                _currentPoiPage = 1;
                Stalls.ReplaceRange(Array.Empty<StallItem>());
                UpdatePoiPagination(1);
                return;
            }

            var queryTerms = SplitSearchTerms(_searchText);
            var filtered = _nearbyStalls
                .Where(stall => MatchesSearch(stall, queryTerms))
                .ToList();

            DisplayVisibleStalls(filtered, resetPage: true);
        }

        private async Task DebouncedBackendSearchAsync(string query, CancellationToken cancellationToken)
        {
            try
            {
                if (!CanAttemptBackendRequest())
                {
                    return;
                }

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
                    .Select(UpdateDistanceFromCurrentLocation)
                    .OrderBy(stall => stall.Distance <= 0 ? double.MaxValue : stall.Distance)
                    .ThenBy(stall => stall.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (!string.IsNullOrWhiteSpace(_searchText))
                    {
                        DisplayVisibleStalls(_remoteSearchStalls, resetPage: true);
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

        private void DisplayVisibleStalls(IEnumerable<StallItem> stalls, bool resetPage = false)
        {
            _visibleStallSource = stalls
                .OrderBy(stall => stall.Distance <= 0 ? double.MaxValue : stall.Distance)
                .ThenBy(stall => stall.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            if (resetPage)
            {
                _currentPoiPage = 1;
            }

            RenderVisibleStallsPage();
        }

        private void RenderVisibleStallsPage()
        {
            var totalPages = Math.Max(1, (int)Math.Ceiling(_visibleStallSource.Count / (double)PoiPageSize));
            _currentPoiPage = Math.Min(Math.Max(1, _currentPoiPage), totalPages);

            var highlighted = _visibleStallSource
                .Skip((_currentPoiPage - 1) * PoiPageSize)
                .Take(PoiPageSize)
                .Select(stall => ApplyHighlight(stall, _searchText))
                .ToList();

            if (!AreVisibleStallsEquivalent(highlighted))
            {
                Stalls.ReplaceRange(highlighted);
            }

            UpdatePoiPagination(totalPages);
        }

        private void UpdatePoiPagination(int totalPages)
        {
            PoiPaginationBar.IsVisible = _visibleStallSource.Count > PoiPageSize;
            PoiPrevButton.IsVisible = _currentPoiPage > 1;
            PoiNextButton.IsVisible = _currentPoiPage < totalPages;
            PoiPrevButton.IsEnabled = PoiPrevButton.IsVisible;
            PoiNextButton.IsEnabled = PoiNextButton.IsVisible;
            PoiPrevButton.Opacity = PoiPrevButton.IsVisible ? 1 : 0;
            PoiNextButton.Opacity = PoiNextButton.IsVisible ? 1 : 0;

            PoiPageNumbersLayout.Children.Clear();
            if (totalPages <= 1)
            {
                return;
            }

            foreach (var item in BuildPoiPaginationItems(totalPages))
            {
                if (item is null)
                {
                    PoiPageNumbersLayout.Children.Add(new Label
                    {
                        Text = "...",
                        FontSize = 10,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#8C5A27"),
                        VerticalTextAlignment = TextAlignment.Center,
                        HorizontalTextAlignment = TextAlignment.Center,
                        WidthRequest = 16
                    });
                    continue;
                }

                var targetPage = item.Value;
                var button = new Button
                {
                    Text = targetPage.ToString(CultureInfo.InvariantCulture),
                    Padding = new Thickness(6, 3),
                    MinimumWidthRequest = 30,
                    HeightRequest = 26,
                    CornerRadius = 9,
                    FontSize = 10,
                    FontAttributes = FontAttributes.Bold,
                    BackgroundColor = targetPage == _currentPoiPage ? Color.FromArgb("#E07828") : Color.FromArgb("#F6EFE7"),
                    TextColor = targetPage == _currentPoiPage ? Colors.White : Color.FromArgb("#8C5A27")
                };
                button.Clicked += (_, _) => GoToPoiPage(targetPage);
                PoiPageNumbersLayout.Children.Add(button);
            }
        }

        private IEnumerable<int?> BuildPoiPaginationItems(int totalPages)
        {
            if (totalPages <= 3)
            {
                for (var page = 1; page <= totalPages; page++)
                {
                    yield return page;
                }

                yield break;
            }

            if (_currentPoiPage <= 2)
            {
                if (_currentPoiPage > 1)
                {
                    yield return null;
                }
                yield return 1;
                yield return 2;
                yield return 3;
                yield return null;
                yield break;
            }

            if (_currentPoiPage >= totalPages - 1)
            {
                yield return null;
                yield return totalPages - 2;
                yield return totalPages - 1;
                yield return totalPages;
                if (_currentPoiPage < totalPages)
                {
                    yield return null;
                }
                yield break;
            }

            yield return null;
            yield return _currentPoiPage - 1;
            yield return _currentPoiPage;
            yield return _currentPoiPage + 1;
            yield return null;
        }

        private void GoToPoiPage(int page)
        {
            if (page <= 0 || page == _currentPoiPage)
            {
                return;
            }

            _currentPoiPage = page;
            RenderVisibleStallsPage();
        }

        private void OnPoiPrevClicked(object sender, EventArgs e)
        {
            if (_currentPoiPage <= 1)
            {
                return;
            }

            _currentPoiPage = 1;
            RenderVisibleStallsPage();
        }

        private void OnPoiNextClicked(object sender, EventArgs e)
        {
            var totalPages = Math.Max(1, (int)Math.Ceiling(_visibleStallSource.Count / (double)PoiPageSize));
            if (_currentPoiPage >= totalPages)
            {
                return;
            }

            _currentPoiPage = totalPages;
            RenderVisibleStallsPage();
        }

        private bool AreVisibleStallsEquivalent(IReadOnlyList<StallItem> candidate)
        {
            if (candidate.Count != Stalls.Count)
            {
                return false;
            }

            for (var index = 0; index < candidate.Count; index++)
            {
                var current = Stalls[index];
                var next = candidate[index];
                if (current.Id != next.Id ||
                    !string.Equals(current.Name, next.Name, StringComparison.Ordinal) ||
                    !string.Equals(current.Cuisine, next.Cuisine, StringComparison.Ordinal) ||
                    !string.Equals(current.Rating, next.Rating, StringComparison.Ordinal) ||
                    !string.Equals(current.Reviews, next.Reviews, StringComparison.Ordinal) ||
                    !string.Equals(current.DistanceText, next.DistanceText, StringComparison.Ordinal) ||
                    Math.Abs(current.Distance - next.Distance) > 0.005 ||
                    !string.Equals(current.ThumbnailUrl, next.ThumbnailUrl, StringComparison.Ordinal) ||
                    !string.Equals(current.ImageUrl, next.ImageUrl, StringComparison.Ordinal) ||
                    current.HasOfflineAudio != next.HasOfflineAudio)
                {
                    return false;
                }
            }

            return true;
        }

        private void OnSearchButtonPressed(object sender, EventArgs e)
        {
            DismissSearchKeyboard();
        }

        private void OnPageBackgroundTapped(object sender, TappedEventArgs e)
        {
            DismissSearchKeyboard();
        }

        private void DismissSearchKeyboard()
        {
            if (SearchBarControl.IsFocused)
            {
                SearchBarControl.Unfocus();
            }
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
            DismissSearchKeyboard();
            StopSpeech();
            StopAudioPlayback();
            _currentPopupStall = null;
            _isScriptExpanded = false;
            ScriptPopupFadeOverlay.IsVisible = false;
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

            _poiGeofenceEngine.Reset();
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
                    var stallSnapshot = await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        if (_nearbyStalls.Count > 0)
                        {
                            return _nearbyStalls.ToList();
                        }

                        return Stalls.ToList();
                    });
                    if (status == PermissionStatus.Granted && stallSnapshot.Count > 0)
                    {
                        var location = await GetMonitoringLocationAsync(cancellationToken);
                        if (location != null)
                        {
                            var previousLocation = _lastKnownLocation;
                            var monitoringDecision = _gpsMonitoringPolicy.Evaluate(
                                previousLocation,
                                location,
                                _lastNearbyFetchLocation,
                                _nearbyStalls.Count > 0 && CanAttemptBackendRequest());
                            _lastKnownLocation = location;

                            if (monitoringDecision.ShouldUpdateUi)
                            {
                                MainThread.BeginInvokeOnMainThread(() => RefreshDistancesAndOrdering(location));
                                _ = _stallService.LogLocationPingAsync(location);
                                await CheckPoiForLocationAsync(location, stallSnapshot, cancellationToken);

                                if (monitoringDecision.ShouldRefreshNearbyStalls)
                                {
                                    _lastNearbyFetchLocation = location;
                                    _ = LoadDataFromServer(preferResponsiveLocation: true);
                                }
                            }
                        }
                        else
                        {
                            Debug.WriteLine("--- POI KHONG CO DU LIEU GPS de kiem tra tu dong phat");
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
            var enteredPoi = _poiGeofenceEngine.Evaluate(userLocation, stalls, DateTime.UtcNow);
            if (enteredPoi is null || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (ScriptPopupOverlay.IsVisible || _currentPopupStall is not null)
            {
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(() => ShowScriptPopupAsync(enteredPoi));
        }

        private async Task<Location?> GetBestAvailableLocationAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var lastKnown = await Geolocation.Default.GetLastKnownLocationAsync();
                if (IsAcceptableLocation(lastKnown, FreshLastKnownLocationWindow, AcceptableLastKnownAccuracyMeters))
                {
                    return lastKnown;
                }
            }
            catch
            {
                // Fall through to active sampling.
            }

            Location? bestLocation = null;

            for (var index = 0; index < PreciseLocationSampleCount; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Location? sample = null;
                try
                {
                    var request = new GeolocationRequest(GeolocationAccuracy.Best, PreciseLocationTimeout);
                    sample = await Geolocation.Default.GetLocationAsync(request, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    // Best-effort sampling only.
                }

                if (sample != null && IsBetterLocation(sample, bestLocation))
                {
                    bestLocation = sample;
                    if ((sample.Accuracy ?? double.MaxValue) <= DesiredPoiAccuracyMeters)
                    {
                        break;
                    }
                }

                if (index < PreciseLocationSampleCount - 1)
                {
                    await Task.Delay(PreciseLocationSampleDelay, cancellationToken);
                }
            }

            return bestLocation;
        }

        private async Task<Location?> GetResponsiveLocationAsync(CancellationToken cancellationToken = default)
        {
            if (IsAcceptableLocation(_lastKnownLocation, FastRefreshLocationWindow, FastRefreshAcceptedAccuracyMeters))
            {
                return _lastKnownLocation;
            }

            if (IsAcceptableLocation(_lastKnownLocation, SoftRefreshLocationWindow, SoftRefreshAcceptedAccuracyMeters))
            {
                return _lastKnownLocation;
            }

            try
            {
                var lastKnown = await Geolocation.Default.GetLastKnownLocationAsync();
                if (IsAcceptableLocation(lastKnown, FastRefreshLocationWindow, FastRefreshAcceptedAccuracyMeters))
                {
                    return lastKnown;
                }

                if (IsAcceptableLocation(lastKnown, SoftRefreshLocationWindow, SoftRefreshAcceptedAccuracyMeters))
                {
                    return lastKnown;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Fall back to the last in-memory location below.
            }

            return _lastKnownLocation;
        }

        private async Task<Location?> GetMonitoringLocationAsync(CancellationToken cancellationToken = default)
        {
            Location? fallbackLocation = null;

            if (IsAcceptableLocation(_lastKnownLocation, SoftRefreshLocationWindow, SoftRefreshAcceptedAccuracyMeters))
            {
                fallbackLocation = _lastKnownLocation;
            }

            try
            {
                var lastKnown = await Geolocation.Default.GetLastKnownLocationAsync();
                if (lastKnown != null && IsBetterLocation(lastKnown, fallbackLocation))
                {
                    fallbackLocation = lastKnown;
                }

                if (IsAcceptableLocation(lastKnown, PoiMonitorCachedLocationWindow, PoiMonitorAcceptedAccuracyMeters))
                {
                    return lastKnown;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Fall through to an active location request.
            }

            try
            {
                var request = new GeolocationRequest(GeolocationAccuracy.Best, PoiMonitorActiveLocationTimeout);
                var liveLocation = await Geolocation.Default.GetLocationAsync(request, cancellationToken);
                if (liveLocation != null)
                {
                    return IsBetterLocation(liveLocation, fallbackLocation) ? liveLocation : fallbackLocation ?? liveLocation;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Monitoring should stay best-effort.
            }

            return fallbackLocation;
        }

        private static bool IsAcceptableLocation(Location? location, TimeSpan maxAge, double maxAccuracyMeters)
        {
            if (location is null)
            {
                return false;
            }

            if (location.Timestamp == default)
            {
                return false;
            }

            if (DateTimeOffset.UtcNow - location.Timestamp > maxAge)
            {
                return false;
            }

            return (location.Accuracy ?? double.MaxValue) <= maxAccuracyMeters;
        }

        private static bool IsBetterLocation(Location candidate, Location? currentBest)
        {
            if (currentBest is null)
            {
                return true;
            }

            var candidateAccuracy = candidate.Accuracy ?? double.MaxValue;
            var currentAccuracy = currentBest.Accuracy ?? double.MaxValue;

            if (candidateAccuracy + 1 < currentAccuracy)
            {
                return true;
            }

            if (Math.Abs(candidateAccuracy - currentAccuracy) <= 1)
            {
                return candidate.Timestamp > currentBest.Timestamp;
            }

            return false;
        }

        private void ScheduleNearbyAudioPreload(IEnumerable<StallItem> stalls)
        {
            _deferredAudioPreloadCts?.Cancel();
            _deferredAudioPreloadCts?.Dispose();

            var snapshot = stalls
                .Where(item => item.Id > 0)
                .Take(3)
                .ToList();

            if (snapshot.Count == 0)
            {
                _deferredAudioPreloadCts = null;
                return;
            }

            var cts = new CancellationTokenSource();
            _deferredAudioPreloadCts = cts;
            _ = PreloadNearbyAudioAsync(snapshot, cts.Token);
        }

        private async Task PreloadNearbyAudioAsync(IEnumerable<StallItem> stalls, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(DeferredAudioPreloadDelay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            var languageCode = GetLanguageCode(_selectedLanguage);
            var cachedIds = await _audioCacheService.PreloadTopStallsAsync(stalls, languageCode, limit: 3, cancellationToken);
            if (cachedIds.Count == 0)
            {
                return;
            }

            MainThread.BeginInvokeOnMainThread(() => RefreshOfflineFlags(cachedIds));
        }

        private async Task HandleQrScanResultAsync(string rawValue)
        {
            var text = AppText.Get(_selectedLanguage);
            var qrCodeValue = ExtractQrCodeValue(rawValue);
            if (string.IsNullOrWhiteSpace(qrCodeValue))
            {
                await DisplayAlert(text.QrTitle, text.QrInvalidMessage, "OK");
                return;
            }

            var localCandidates = _nearbyStalls.Count > 0
                ? _nearbyStalls
                : await _stallService.LoadCachedStallsAsync();

            var localMatch = _stallService.TryResolveQrLocally(qrCodeValue, localCandidates);
            if (localMatch is not null)
            {
                await ShowScriptPopupAsync(LocalizeStall(AttachOfflineFlag(localMatch)));
                return;
            }

            var stall = await _stallService.ResolveQrAsync(
                qrCodeValue,
                _lastKnownLocation?.Latitude,
                _lastKnownLocation?.Longitude);

            if (stall is null)
            {
                await DisplayAlert(text.QrTitle, text.QrNotFoundMessage, "OK");
                return;
            }

            stall = LocalizeStall(AttachOfflineFlag(stall));
            await ShowScriptPopupAsync(stall);
        }

        private List<StallItem> PrepareMapStalls(IEnumerable<StallItem> stalls)
        {
            if (ReferenceEquals(stalls, _nearbyStalls))
            {
                if (_localizedSnapshotCount == _nearbyStalls.Count &&
                    string.Equals(_localizedSnapshotLanguage, _selectedLanguage, StringComparison.Ordinal) &&
                    _localizedNearbySnapshot.Count > 0)
                {
                    return _localizedNearbySnapshot;
                }

                _localizedSnapshotLanguage = _selectedLanguage;
                _localizedSnapshotCount = _nearbyStalls.Count;
                _localizedNearbySnapshot = new List<StallItem>(_nearbyStalls);
                return _localizedNearbySnapshot;
            }

            return stalls
                .Select(AttachOfflineFlag)
                .Select(LocalizeStall)
                .ToList();
        }

        private void InvalidateLocalizedSnapshots()
        {
            _localizedSnapshotLanguage = string.Empty;
            _localizedSnapshotCount = -1;
            _localizedNearbySnapshot = new List<StallItem>();
        }

        private async Task RefreshFromServerIfChangedAsync()
        {
            if (_isRefreshingFromServer || !CanAttemptBackendRequest())
            {
                return;
            }

            var utcNow = DateTime.UtcNow;
            if (utcNow - _lastSyncCheckUtc < BackgroundSyncInterval)
            {
                return;
            }

            _lastSyncCheckUtc = utcNow;

            try
            {
                var latestVersion = await _stallService.GetSyncVersionAsync();
                if (string.IsNullOrWhiteSpace(latestVersion))
                {
                    return;
                }

                if (string.Equals(_lastKnownSyncVersion, latestVersion, StringComparison.Ordinal))
                {
                    return;
                }

                await LoadDataFromServer(preferResponsiveLocation: true);
            }
            catch
            {
            }
        }

        private void StartBackgroundSync()
        {
            if (_backgroundSyncCts is not null)
            {
                return;
            }

            _backgroundSyncCts = new CancellationTokenSource();
            _ = RunBackgroundSyncLoopAsync(_backgroundSyncCts.Token);
        }

        private void StopBackgroundSync()
        {
            _backgroundSyncCts?.Cancel();
            _backgroundSyncCts?.Dispose();
            _backgroundSyncCts = null;
        }

        private async Task RunBackgroundSyncLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(BackgroundSyncInterval, cancellationToken);
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    await RefreshFromServerIfChangedAsync();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                }
            }
        }

        private void EnsureConnectivitySubscription()
        {
            if (_isSubscribedToConnectivityChanges)
            {
                return;
            }

            Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
            _isSubscribedToConnectivityChanges = true;
        }

        private void RemoveConnectivitySubscription()
        {
            if (!_isSubscribedToConnectivityChanges)
            {
                return;
            }

            Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
            _isSubscribedToConnectivityChanges = false;
        }

        private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
        {
            if (e.NetworkAccess != NetworkAccess.Internet)
            {
                return;
            }

            _lastSyncCheckUtc = DateTime.MinValue;
            MainThread.BeginInvokeOnMainThread(() => _ = RefreshFromServerIfChangedAsync());
        }

        private static string? ExtractQrCodeValue(string rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return null;
            }

            var trimmed = rawValue.Trim();
            if (trimmed.StartsWith("sfqr1.", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }

            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            {
                var queryCode = GetQueryParameter(uri.Query, "code");
                if (!string.IsNullOrWhiteSpace(queryCode))
                {
                    return queryCode;
                }

                var lastSegment = uri.Segments.LastOrDefault()?.Trim('/');
                if (!string.IsNullOrWhiteSpace(lastSegment) && lastSegment.StartsWith("sfqr1.", StringComparison.OrdinalIgnoreCase))
                {
                    return lastSegment;
                }
            }

            return null;
        }

        private static string? GetQueryParameter(string query, string key)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return null;
            }

            foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split('=', 2);
                if (parts.Length == 2 && string.Equals(parts[0], key, StringComparison.OrdinalIgnoreCase))
                {
                    return Uri.UnescapeDataString(parts[1]);
                }
            }

            return null;
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

        private void RenderScriptPopup(StallItem stall, LocalizedText text, string languageCode)
        {
            var content = stall.GetScript(languageCode);
            ScriptPopupHeaderLabel.Text = text.ScriptDialogTitle;
            ScriptPopupTitleLabel.Text = stall.Name;
            ScriptPopupImage.Source = ResolvePopupImageSource(stall);
            ScriptPopupCuisineLabel.Text = $"{text.PopupCuisineLabel}: {stall.Cuisine}";
            ScriptPopupDistanceLabel.Text = $"{text.PopupDistanceLabel}: {stall.DistanceText}";
            UpdatePopupRatingSummary(stall);
            var hoursText = stall.GetDisplayHours();
            ScriptPopupHoursLabel.Text = $"{text.PopupHoursLabel}: {(string.IsNullOrWhiteSpace(hoursText) ? "-" : hoursText)}";
            ScriptPopupSpecialtiesHeaderLabel.Text = text.PopupSpecialtiesLabel;
            PopulateSpecialties(stall.GetTopSpecialties(languageCode));
            ScriptPopupContentLabel.Text = content;
            ConfigureScriptExpansion(content, text);
            ScriptPopupCloseButton.Text = text.NavigateToStallText;
            _currentPopupStall = stall;
            UpdatePopupRatingButtons();
            UpdateAudioStatusUi(stall, text, languageCode);
            ScriptPopupOverlay.IsVisible = true;
            MainThread.BeginInvokeOnMainThread(async () => await ScriptPopupScrollView.ScrollToAsync(0, 0, false));
        }

        private static string? ResolvePopupImageSource(StallItem stall)
        {
            if (!string.IsNullOrWhiteSpace(stall.ThumbnailUrl))
            {
                return stall.ThumbnailUrl;
            }

            return string.IsNullOrWhiteSpace(stall.ImageUrl) ? null : stall.ImageUrl;
        }

        private void UpdatePopupRatingSummary(StallItem stall)
        {
            var ratingValue = stall.GetRatingValue();
            var reviewsCount = stall.GetReviewsCount();
            var filledStarCount = (int)Math.Round(Math.Clamp(ratingValue, 0, 5), MidpointRounding.AwayFromZero);
            var stars = new string('★', filledStarCount).PadRight(5, '☆');
            ScriptPopupRatingSummaryLabel.Text = $"{stars} {ratingValue:0.0} ({reviewsCount})";
        }

        private void UpdatePopupRatingButtons()
        {
            var text = AppText.Get(_selectedLanguage);
            var hasRatedCurrentStall = HasRatedCurrentPopupStall();
            ScriptPopupRatePromptLabel.Text = hasRatedCurrentStall
                ? text.RatingAlreadySubmittedMessage
                : text.RateThisStallText;

            foreach (var button in ScriptPopupRatingButtons.Children.OfType<Button>())
            {
                button.IsEnabled = !_isSubmittingRating && !hasRatedCurrentStall;
                button.Opacity = (_isSubmittingRating || hasRatedCurrentStall) ? 0.55 : 1;
            }
        }

        private void UpdateRefreshButtonState()
        {
            var text = AppText.Get(_selectedLanguage);
            RefreshDataButton.IsEnabled = !_isRefreshingFromServer;
            RefreshDataButton.Text = _isRefreshingFromServer ? text.RefreshingDataText : text.RefreshDataText;
            RefreshDataButton.Opacity = _isRefreshingFromServer ? 0.75 : 1;
        }

        private StallItem? GetLatestStallSnapshot(StallItem stall)
        {
            if (stall.Id <= 0)
            {
                return stall;
            }

            return _nearbyStalls.FirstOrDefault(item => item.Id == stall.Id)
                ?? _remoteSearchStalls.FirstOrDefault(item => item.Id == stall.Id)
                ?? Stalls.FirstOrDefault(item => item.Id == stall.Id);
        }

        private void UpdateNearbySnapshot(StallItem refreshed)
        {
            var nearbyIndex = _nearbyStalls.FindIndex(item => item.Id == refreshed.Id);
            if (nearbyIndex >= 0)
            {
                _nearbyStalls[nearbyIndex] = refreshed;
                _nearbyStalls = _nearbyStalls
                    .OrderBy(stall => stall.Distance <= 0 ? double.MaxValue : stall.Distance)
                    .ThenBy(stall => stall.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            }

            var remoteIndex = _remoteSearchStalls.FindIndex(item => item.Id == refreshed.Id);
            if (remoteIndex >= 0)
            {
                _remoteSearchStalls[remoteIndex] = refreshed;
                _remoteSearchStalls = _remoteSearchStalls
                    .OrderBy(stall => stall.Distance <= 0 ? double.MaxValue : stall.Distance)
                    .ThenBy(stall => stall.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            }

            var activeSource = string.IsNullOrWhiteSpace(_searchText)
                ? _nearbyStalls
                : (_remoteSearchStalls.Count > 0
                    ? _remoteSearchStalls
                    : _nearbyStalls.Where(stall => MatchesSearch(stall, SplitSearchTerms(_searchText))));

            DisplayVisibleStalls(activeSource);

            InvalidateLocalizedSnapshots();
        }

        private void RefreshCurrentPopupIfNeeded()
        {
            if (!ScriptPopupOverlay.IsVisible || _currentPopupStall is null)
            {
                return;
            }

            var refreshed = GetLatestStallSnapshot(_currentPopupStall);
            if (refreshed is null)
            {
                return;
            }

            var languageCode = GetLanguageCode(_selectedLanguage);
            var previousScript = _currentPopupStall.GetScript(languageCode);
            var refreshedScript = refreshed.GetScript(languageCode);
            var scriptChanged = !string.Equals(previousScript, refreshedScript, StringComparison.Ordinal);

            if (scriptChanged)
            {
                StopSpeech();
                StopAudioPlayback();
            }

            RenderScriptPopup(refreshed, AppText.Get(_selectedLanguage), languageCode);
        }

        private async void OnRatePopupClicked(object sender, EventArgs e)
        {
            if (_currentPopupStall is null || _isSubmittingRating || sender is not Button button)
            {
                return;
            }

            if (!int.TryParse(button.CommandParameter?.ToString(), out var rating) || rating < 1 || rating > 5)
            {
                return;
            }

            var text = AppText.Get(_selectedLanguage);
            var stallId = _currentPopupStall.Id;
            if (HasRatedStall(stallId))
            {
                await DisplayAlert(text.PopupRatingLabel, text.RatingAlreadySubmittedMessage, "OK");
                UpdatePopupRatingButtons();
                return;
            }

            var confirmed = await DisplayAlert(
                text.PopupRatingLabel,
                string.Format(text.RatingConfirmMessage, rating),
                text.RatingConfirmText,
                text.CancelText);
            if (!confirmed)
            {
                return;
            }

            _isSubmittingRating = true;
            UpdatePopupRatingButtons();

            try
            {
                var refreshed = await _stallService.SubmitRatingAsync(stallId, rating, _lastKnownLocation);
                if (refreshed is null)
                {
                    throw new InvalidOperationException(text.RatingSubmitFailedMessage);
                }

                MarkStallAsRated(stallId);
                var refreshedWithDistance = PreserveDistanceIfMissing(_currentPopupStall, refreshed);
                var localized = AttachOfflineFlag(LocalizeStall(refreshedWithDistance));
                UpdateNearbySnapshot(localized);
                RenderScriptPopup(localized, text, GetLanguageCode(_selectedLanguage));
                await DisplayAlert(text.PopupRatingLabel, text.RatingSubmittedMessage, "OK");
            }
            catch (Exception ex)
            {
                var message = string.IsNullOrWhiteSpace(ex.Message) ? text.RatingSubmitFailedMessage : ex.Message;
                if (IsDuplicateRatingMessage(message))
                {
                    MarkStallAsRated(stallId);
                    UpdatePopupRatingButtons();
                }

                await DisplayAlert(text.ErrorTitle, message, "OK");
            }
            finally
            {
                _isSubmittingRating = false;
                UpdatePopupRatingButtons();
            }
        }

        private bool HasRatedCurrentPopupStall()
        {
            return _currentPopupStall is { Id: > 0 } stall && HasRatedStall(stall.Id);
        }

        private static StallItem PreserveDistanceIfMissing(StallItem currentStall, StallItem refreshedStall)
        {
            if (refreshedStall.Distance > 0 || !string.IsNullOrWhiteSpace(refreshedStall.DistanceText))
            {
                return refreshedStall;
            }

            return refreshedStall with
            {
                Distance = currentStall.Distance,
                DistanceText = currentStall.DistanceText
            };
        }

        private void RefreshDistancesAndOrdering(Location location)
        {
            _nearbyStalls = _nearbyStalls
                .Select(stall => UpdateDistanceForLocation(stall, location))
                .OrderBy(stall => stall.Distance <= 0 ? double.MaxValue : stall.Distance)
                .ThenBy(stall => stall.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            if (_remoteSearchStalls.Count > 0)
            {
                _remoteSearchStalls = _remoteSearchStalls
                    .Select(stall => UpdateDistanceForLocation(stall, location))
                    .OrderBy(stall => stall.Distance <= 0 ? double.MaxValue : stall.Distance)
                    .ThenBy(stall => stall.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            }

            var activeSource = string.IsNullOrWhiteSpace(_searchText)
                ? _nearbyStalls
                : (_remoteSearchStalls.Count > 0
                    ? _remoteSearchStalls
                    : _nearbyStalls.Where(stall => MatchesSearch(stall, SplitSearchTerms(_searchText))));

            DisplayVisibleStalls(activeSource);
            RefreshCurrentPopupIfNeeded();
        }

        private StallItem UpdateDistanceFromCurrentLocation(StallItem stall)
        {
            return _lastKnownLocation is null ? stall : UpdateDistanceForLocation(stall, _lastKnownLocation);
        }

        private static StallItem UpdateDistanceForLocation(StallItem stall, Location location)
        {
            if (stall.Lat == 0 || stall.Lng == 0)
            {
                return stall;
            }

            var distanceKm = CalculateDistanceKm(location.Latitude, location.Longitude, stall.Lat, stall.Lng);
            return stall with
            {
                Distance = distanceKm,
                DistanceText = FormatDistanceText(distanceKm)
            };
        }

        private static double CalculateDistanceKm(double lat1, double lng1, double lat2, double lng2)
        {
            const double earthRadiusKm = 6371d;
            var dLat = DegreesToRadians(lat2 - lat1);
            var dLng = DegreesToRadians(lng2 - lng1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                    + Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2))
                    * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return earthRadiusKm * c;
        }

        private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;

        private static string FormatDistanceText(double distanceKm)
        {
            if (distanceKm <= 0)
            {
                return "0m";
            }

            var meters = distanceKm * 1000d;
            if (meters < 1000)
            {
                return $"{Math.Round(meters, MidpointRounding.AwayFromZero):0}m";
            }

            return $"{distanceKm:0.0}km";
        }

        private bool HasRatedStall(int stallId)
        {
            return stallId > 0 && _ratedStallIds.Contains(stallId);
        }

        private void MarkStallAsRated(int stallId)
        {
            if (stallId <= 0 || !_ratedStallIds.Add(stallId))
            {
                return;
            }

            Preferences.Default.Set(RatedStallIdsPreferenceKey, string.Join(",", _ratedStallIds.OrderBy(id => id)));
        }

        private void LoadRatedStallIds()
        {
            _ratedStallIds.Clear();
            var rawValue = Preferences.Default.Get(RatedStallIdsPreferenceKey, string.Empty);
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return;
            }

            foreach (var token in rawValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(token, out var stallId) && stallId > 0)
                {
                    _ratedStallIds.Add(stallId);
                }
            }
        }

        private static bool IsDuplicateRatingMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            return message.Contains("đánh giá gian hàng này một lần", StringComparison.OrdinalIgnoreCase)
                || message.Contains("already rated this stall", StringComparison.OrdinalIgnoreCase)
                || message.Contains("rate this stall once", StringComparison.OrdinalIgnoreCase);
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
            var hasCachedAudio = _audioCacheService.HasCachedAudio(stall, languageCode);
            ScriptPopupAudioStatusLabel.Text = hasCachedAudio ? text.AudioReadyLabel : text.AudioNotReadyLabel;
            ScriptPopupAudioStatusLabel.TextColor = hasCachedAudio ? Color.FromArgb("#2E8F52") : Color.FromArgb("#607086");
            ScriptPopupDownloadButton.Text = text.DownloadAudioButton;
            ScriptPopupDownloadButton.IsVisible = !hasCachedAudio && stall.Id > 0;
            ScriptPopupDownloadButton.IsEnabled = stall.Id > 0;
        }

        private void ConfigureScriptExpansion(string content, LocalizedText text)
        {
            var shouldShowToggle = !string.IsNullOrWhiteSpace(content) && content.Length > 220;
            _isScriptExpanded = false;
            ScriptPopupReadMoreButton.IsVisible = shouldShowToggle;
            ScriptPopupReadMoreButton.Text = text.ReadMoreText;
            ScriptPopupContentLabel.MaxLines = shouldShowToggle ? 6 : int.MaxValue;
            ScriptPopupFadeOverlay.IsVisible = shouldShowToggle;
        }

        private void UpdateScriptExpansionUi()
        {
            var text = AppText.Get(_selectedLanguage);
            ScriptPopupContentLabel.MaxLines = _isScriptExpanded ? int.MaxValue : 6;
            ScriptPopupReadMoreButton.Text = _isScriptExpanded ? text.ReadLessText : text.ReadMoreText;
            ScriptPopupFadeOverlay.IsVisible = !_isScriptExpanded && ScriptPopupReadMoreButton.IsVisible;
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

            var success = await _audioCacheService.PreloadAudioAsync(_currentPopupStall, languageCode);
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

        private static bool CanAttemptBackendRequest()
        {
            return Connectivity.Current.NetworkAccess != NetworkAccess.None;
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

        private static void EnsureAndroidBackgroundTracking()
        {
            var context = Android.App.Application.Context;
            var intent = new Intent(context, typeof(BackgroundLocationTrackingService));
            StartAndroidBackgroundTracking(context, intent);
        }

        private static void StartAndroidBackgroundTracking(Android.Content.Context context, Intent intent)
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                StartAndroidForegroundService(context, intent);
                return;
            }

            context.StartService(intent);
        }

        [SupportedOSPlatform("android26.0")]
        private static void StartAndroidForegroundService(Android.Content.Context context, Intent intent)
        {
            context.StartForegroundService(intent);
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
