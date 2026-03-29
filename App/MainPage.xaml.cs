using System.Collections.ObjectModel;
using System.Diagnostics;
using FoodStreetAudioGuide.Models;

namespace FoodStreetAudioGuide
{
    public partial class MainPage : ContentPage
    {
        private readonly StallService _stallService;
        private string _selectedLanguage;
        private string _awayLabel = "AWAY";

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

        public MainPage(StallService stallService, string selectedLanguage = AppText.Vietnamese)
        {
            InitializeComponent();

            _stallService = stallService;
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

        private async Task LoadDataFromServer()
        {
            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                }

                Location location = null;
                if (status == PermissionStatus.Granted)
                {
                    var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(5));
                    location = await Geolocation.Default.GetLocationAsync(request);
                }

                List<StallItem> data = null;
                if (location != null)
                {
                    data = await _stallService.GetNearbyStallsAsync(location.Latitude, location.Longitude);
                }
                else
                {
                    Debug.WriteLine("--- KHÔNG LẤY ĐƯỢC GPS: Thử gọi API với tọa độ mặc định hoặc hiện Mock Data");
                    data = await _stallService.GetNearbyStallsAsync(10.7626, 106.7064);
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Stalls.Clear();

                    if (data != null && data.Count > 0)
                    {
                        foreach (var item in data)
                        {
                            Stalls.Add(item);
                        }
                    }
                    else
                    {
                        LoadMockData();
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"--- LỖI TẠI MAINPAGE: {ex.Message}");
                MainThread.BeginInvokeOnMainThread(LoadMockData);
            }
        }

        private void LoadMockData()
        {
            Stalls.Clear();

            Stalls.Add(new StallItem(
                "50m",
                "Ốc Vĩnh Khánh (Demo)",
                "4.8",
                "(120)",
                "Hải sản",
                new Dictionary<string, string>
                {
                    ["vi"] = "Chào mừng bạn đến với thiên đường ốc Quận 4",
                    ["en"] = "Welcome to snail paradise D4",
                    ["ko"] = "D4 달팽이 요리의 천국에 오신 것을 환영합니다",
                    ["ja"] = "4区の貝料理パラダイスへようこそ",
                    ["zh-CN"] = "欢迎来到第四郡的螺肉美食天堂"
                }
            ));

            Debug.WriteLine("--- ĐÃ TẢI DỮ LIỆU MOCK THÀNH CÔNG");
        }

        private async void OnStallTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is StallItem stall)
            {
                await PlayAudio(stall);
            }
        }

        private async Task PlayAudio(StallItem stall)
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
                    await PlayAudio(stall);
                }

                return;
            }

            try
            {
                var localeCode = GetLocaleCode(_selectedLanguage);
                var locales = await TextToSpeech.Default.GetLocalesAsync();
                var locale = locales.FirstOrDefault(l =>
                    l.Language.StartsWith(localeCode.Split('-')[0], StringComparison.OrdinalIgnoreCase));

                await TextToSpeech.Default.SpeakAsync(content, new SpeechOptions { Locale = locale });
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
                Application.Current.MainPage = new NavigationPage(new LanguageSelectionPage(_stallService));
            }
        }
    }
}
