namespace FoodStreetAudioGuide
{
    public partial class MainPage : ContentPage
    {
        public Command BackCommand { get; }

        public MainPage(string selectedLanguage = "Tiếng Việt")
        {
            BackCommand = new Command(async () => await NavigateBackAsync());

            InitializeComponent();
            BindingContext = this;
            ApplyLanguage(selectedLanguage);
        }

        private void ApplyLanguage(string selectedLanguage)
        {
            switch (selectedLanguage)
            {
                case "English":
                    PageTitleLabel.Text = "Stall List";
                    SearchHintLabel.Text = "Search food stalls, cuisines...";
                    NearbyFilterLabel.Text = "Nearby";
                    OpenNowFilterLabel.Text = "Open Now";
                    TopRatedFilterLabel.Text = "Top Rated";
                    Under200FilterLabel.Text = "Under 200m";
                    ExploreTabLabel.Text = "EXPLORES";
                    MapTabLabel.Text = "MAP";
                    SavedTabLabel.Text = "SAVED";

                    break;
                case "中文 (Chinese)":
                    PageTitleLabel.Text = "摊位列表";
                    SearchHintLabel.Text = "搜索美食摊位、菜系...";
                    NearbyFilterLabel.Text = "附近";
                    OpenNowFilterLabel.Text = "营业中";
                    TopRatedFilterLabel.Text = "高评分";
                    Under200FilterLabel.Text = "200米内";
                    ExploreTabLabel.Text = "探索";
                    MapTabLabel.Text = "地图";
                    SavedTabLabel.Text = "收藏";

                    break;
                case "日本語 (Japanese)":
                    PageTitleLabel.Text = "屋台リスト";
                    SearchHintLabel.Text = "屋台や料理を検索...";
                    NearbyFilterLabel.Text = "近い順";
                    OpenNowFilterLabel.Text = "営業中";
                    TopRatedFilterLabel.Text = "高評価";
                    Under200FilterLabel.Text = "200m以内";
                    ExploreTabLabel.Text = "探索";
                    MapTabLabel.Text = "地図";
                    SavedTabLabel.Text = "保存";

                    break;
                case "한국어 (Korean)":
                    PageTitleLabel.Text = "매장 목록";
                    SearchHintLabel.Text = "음식 매장, 요리를 검색...";
                    NearbyFilterLabel.Text = "가까운 순";
                    OpenNowFilterLabel.Text = "영업 중";
                    TopRatedFilterLabel.Text = "평점 순";
                    Under200FilterLabel.Text = "200m 이내";
                    ExploreTabLabel.Text = "탐색";
                    MapTabLabel.Text = "지도";
                    SavedTabLabel.Text = "저장";
                    break;
                default:
                    PageTitleLabel.Text = "Danh sách quầy";
                    SearchHintLabel.Text = "Tìm quầy ăn, món ăn...";
                    NearbyFilterLabel.Text = "Gần đây";
                    OpenNowFilterLabel.Text = "Đang mở";
                    TopRatedFilterLabel.Text = "Đánh giá cao";
                    Under200FilterLabel.Text = "Dưới 200m";
                    ExploreTabLabel.Text = "KHÁM PHÁ";
                    MapTabLabel.Text = "BẢN ĐỒ";
                    SavedTabLabel.Text = "ĐÃ LƯU";

                    break;
            }

            StallsCollectionView.ItemsSource = new[]
            {
                new StallItem("50m", "Gourmet Ramen", "4.8", "(120)", "Japanese"),
                new StallItem("120m", "Spicy Tacos", "4.5", "(85)", "Mexican"),
                new StallItem("210m", "Neon Pizza", "4.9", "(214)", "Italian"),
                new StallItem("350m", "Zen Bowls", "4.2", "(56)", "Healthy"),
                new StallItem("480m", "The Burger Joint", "4.6", "(310)", "American")
            };
        }

        private async Task NavigateBackAsync()
        {
            if (Navigation.NavigationStack.Count > 1)
            {
                await Navigation.PopAsync();
                return;
            }

            var window = Application.Current?.Windows.FirstOrDefault();
            if (window is not null)
            {
                window.Page = new NavigationPage(new LanguageSelectionPage());
            }
        }

        private sealed record StallItem(string DistanceText, string Name, string Rating, string Reviews, string Cuisine);
    }
}
