using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Maui.Views;
using FoodStreetAudioGuide.Models;
using Microsoft.Maui.Controls.Shapes;

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

        public ObservableCollection<StallItem> Stalls { get; } = new();

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
            await LoadDataFromServer();
        }

        protected override void OnDisappearing()
        {
            StopPoiMonitoring();
            StopSpeechAndHidePopup();
            base.OnDisappearing();
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
                    data = await _stallService.GetNearbyStallsAsync(location.Latitude, location.Longitude);
                }
                else
                {
                    Debug.WriteLine("--- KHONG LAY DUOC GPS: Thu goi API voi toa do mac dinh hoac hien Mock Data");
                    data = await _stallService.GetNearbyStallsAsync(10.7626, 106.7064);
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Stalls.Clear();

                    if (data != null && data.Count > 0)
                    {
                        foreach (var item in data)
                        {
                            Stalls.Add(AttachOfflineFlag(item));
                        }

                        _ = PreloadNearbyAudioAsync(data);
                    }
                    else
                    {
                        LoadMockData();
                    }
                });

                if (data != null && data.Count > 0 && location != null)
                {
                    await CheckPoiForLocationAsync(location, data.Select(AttachOfflineFlag).ToList());
                }

                StartPoiMonitoring();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"--- LOI TAI MAINPAGE: {ex.Message}");
                MainThread.BeginInvokeOnMainThread(LoadMockData);
            }
        }

        private void LoadMockData()
        {
            Stalls.Clear();

            var mock = new StallItem(
                Id: 0,
                DistanceText: "50m",
                Name: "Oc Vinh Khanh (Demo)",
                Rating: "4.8",
                Reviews: "(120)",
                Cuisine: "Hai san",
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

            Stalls.Add(mock);
            Debug.WriteLine("--- DA TAI DU LIEU MOCK THANH CONG");
        }

        private StallItem AttachOfflineFlag(StallItem stall)
        {
            var languageCode = GetLanguageCode(_selectedLanguage);
            return stall with { HasOfflineAudio = _audioCacheService.HasCachedAudio(stall.Id, languageCode) };
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
            RefreshStallOfflineFlags();

            if (!string.IsNullOrWhiteSpace(audioPath))
            {
                PlayCachedAudio(audioPath);
                await _stallService.LogListeningAsync(stall.Id, languageCode, 0);
            }
            else
            {
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
                var localeCode = GetLocaleCode(_selectedLanguage);
                var locales = await TextToSpeech.Default.GetLocalesAsync();
                var locale = locales.FirstOrDefault(l =>
                    l.Language.StartsWith(localeCode.Split('-')[0], StringComparison.OrdinalIgnoreCase));
                var stopwatch = Stopwatch.StartNew();
                await TextToSpeech.Default.SpeakAsync(content, new SpeechOptions { Locale = locale }, cancellationToken);
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
            SearchHintLabel.Text = text.SearchHint;
            ExploreTabLabel.Text = text.ExploreTab;
            MapTabLabel.Text = text.MapTab;
            SavedTabLabel.Text = text.SavedTab;
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
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
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
            await _audioCacheService.PreloadTopStallsAsync(stalls, languageCode);
            MainThread.BeginInvokeOnMainThread(RefreshStallOfflineFlags);
        }

        private void RefreshStallOfflineFlags()
        {
            var refreshed = Stalls.Select(AttachOfflineFlag).ToList();
            Stalls.Clear();
            foreach (var item in refreshed)
            {
                Stalls.Add(item);
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
            if (_speechCts is null)
            {
                return;
            }

            _speechCts.Cancel();
            _speechCts.Dispose();
            _speechCts = null;
        }

        private void PlayCachedAudio(string audioPath)
        {
            StopAudioPlayback();
            ScriptAudioPlayer.Source = MediaSource.FromFile(audioPath);
            ScriptAudioPlayer.Play();
        }

        private void StopAudioPlayback()
        {
            ScriptAudioPlayer.Stop();
            ScriptAudioPlayer.Source = null;
        }

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
            RefreshStallOfflineFlags();

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

        private async Task NavigateBackAsync()
        {
            if (Navigation.NavigationStack.Count > 1)
            {
                await Navigation.PopAsync();
            }
            else
            {
                Application.Current.MainPage = new NavigationPage(new LanguageSelectionPage(_stallService, _audioCacheService));
            }
        }
    }
}
