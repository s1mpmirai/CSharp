using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;

namespace FoodStreetAudioGuide
{
    public partial class MainPage : ContentPage
    {
        public MainPage(string selectedLanguage = "Tiếng Việt")
        {
            InitializeComponent();

            ApplyLanguage(selectedLanguage);

            var (pinLabel, pinAddress) = GetMapTexts(selectedLanguage);

            var pin = new Pin
            {
                Label = pinLabel,
                Location = new Location(10.7684, 106.6803),
                Address = pinAddress
            };

            map.Pins.Add(pin);
        }

        private void ApplyLanguage(string selectedLanguage)
        {
            switch (selectedLanguage)
            {
                case "English":
                    Title = "Food Street";
                    NearbyStallsLabel.Text = "Nearby stalls";
                    StallsCollectionView.ItemsSource = new[]
                    {
                        "1. Huynh Hoa Banh Mi - 50m away 🔊",
                        "2. Nho Snail Stall - 120m away",
                        "3. Y Phuong Thai Dessert - 200m away",
                        "4. Ba Ghien Broken Rice - 350m away"
                    };
                    break;
                case "中文 (Chinese)":
                    Title = "美食街";
                    NearbyStallsLabel.Text = "附近摊位";
                    StallsCollectionView.ItemsSource = new[]
                    {
                        "1. Huynh Hoa 法棍 - 距离 50 米 🔊",
                        "2. Nho 螺蛳摊 - 距离 120 米",
                        "3. Y Phuong 泰式甜品 - 距离 200 米",
                        "4. Ba Ghien 碎米饭 - 距离 350 米"
                    };
                    break;
                case "日本語 (Japanese)":
                    Title = "フードストリート";
                    NearbyStallsLabel.Text = "近くの屋台";
                    StallsCollectionView.ItemsSource = new[]
                    {
                        "1. Huynh Hoa バインミー - 50m先 🔊",
                        "2. Nho スネイル店 - 120m先",
                        "3. Y Phuong タイ風デザート - 200m先",
                        "4. Ba Ghien コムタム - 350m先"
                    };
                    break;
                case "한국어 (Korean)":
                    Title = "푸드 스트리트";
                    NearbyStallsLabel.Text = "주변 매장";
                    StallsCollectionView.ItemsSource = new[]
                    {
                        "1. Huynh Hoa 반미 - 50m 거리 🔊",
                        "2. Nho 달팽이 가게 - 120m 거리",
                        "3. Y Phuong 타이 디저트 - 200m 거리",
                        "4. Ba Ghien 껌땀 - 350m 거리"
                    };
                    break;
                case "Español (Spanish)":
                    Title = "Calle de Comida";
                    NearbyStallsLabel.Text = "Puestos cercanos";
                    StallsCollectionView.ItemsSource = new[]
                    {
                        "1. Bánh mì Huỳnh Hoa - A 50 m 🔊",
                        "2. Puesto de caracoles Nhớ - A 120 m",
                        "3. Postre tailandés Ý Phương - A 200 m",
                        "4. Cơm tấm Ba Ghiền - A 350 m"
                    };
                    break;
                default:
                    Title = "Phố Ẩm Thực";
                    NearbyStallsLabel.Text = "Gian hàng quanh bạn";
                    StallsCollectionView.ItemsSource = new[]
                    {
                        "1. Bánh Mì Huỳnh Hoa - Cách 50m 🔊",
                        "2. Quán Ốc Nhớ - Cách 120m",
                        "3. Chè Thái Ý Phương - Cách 200m",
                        "4. Cơm Tấm Ba Ghiền - Cách 350m"
                    };
                    break;
            }
        }

        private static (string Label, string Address) GetMapTexts(string selectedLanguage)
        {
            return selectedLanguage switch
            {
                "English" => ("Banh Mi Stall", "Playing audio clip..."),
                "中文 (Chinese)" => ("法棍摊位", "正在播放语音..."),
                "日本語 (Japanese)" => ("バインミー屋台", "音声を再生中..."),
                "한국어 (Korean)" => ("반미 가게", "오디오 재생 중..."),
                "Español (Spanish)" => ("Puesto de bánh mì", "Reproduciendo audio..."),
                _ => ("Gian hàng Bánh Mì", "Đang phát đoạn ghi âm...")
            };
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await RequestLocationPermissionAsync();
        }

        private static async Task RequestLocationPermissionAsync()
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
                await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        }
    }
}
