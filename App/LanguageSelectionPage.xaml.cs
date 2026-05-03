using Microsoft.Maui.Controls.Shapes;

namespace FoodStreetAudioGuide
{
    public partial class LanguageSelectionPage : ContentPage
    {
        private const string SelectedLanguagePreferenceKey = "SelectedLanguage";

        private readonly StallService _stallService;
        private readonly AudioCacheService _audioCacheService;
        private string _selectedLanguage;

        // Hàm khởi tạo `LanguageSelectionPage`: thiết lập trạng thái ban đầu cho đối tượng trong file hiện tại.
        public LanguageSelectionPage(StallService stallService, AudioCacheService audioCacheService)
        {
            InitializeComponent();

            _stallService = stallService;
            _audioCacheService = audioCacheService;
            _selectedLanguage = Preferences.Get(SelectedLanguagePreferenceKey, AppText.English);

            ApplyPageText();
            UpdateSelectionUi();
        }

        // Hàm `OnAppearing`: xử lý sự kiện hoặc callback liên quan trong file hiện tại.
        protected override void OnAppearing()
        {
            base.OnAppearing();
            _ = WarmStartupAsync();
        }

        // Hàm `OnLanguageCardTapped`: xử lý sự kiện hoặc callback liên quan trong file hiện tại.
        private void OnLanguageCardTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is not string language)
            {
                return;
            }

            _selectedLanguage = language;
            ApplyPageText();
            UpdateSelectionUi();
        }

        // Hàm `OnContinueClicked`: xử lý sự kiện hoặc callback liên quan trong file hiện tại.
        private async void OnContinueClicked(object sender, EventArgs e)
        {
            Preferences.Set(SelectedLanguagePreferenceKey, _selectedLanguage);
            ContinueButton.IsEnabled = false;
            ContinueButton.Opacity = 0.7;
            try
            {
                await Navigation.PushAsync(new MainPage(_stallService, _audioCacheService, _selectedLanguage), false);
            }
            finally
            {
                ContinueButton.IsEnabled = true;
                ContinueButton.Opacity = 1;
            }
        }

        // Hàm `WarmStartupAsync`: xử lý logic liên quan trong file hiện tại.
        private async Task WarmStartupAsync()
        {
            try
            {
                await _stallService.LoadCachedStallsAsync();
            }
            catch
            {
            }
        }

        // Hàm `ApplyPageText`: áp dụng cấu hình hoặc trạng thái liên quan trong file hiện tại.
        private void ApplyPageText()
        {
            var text = AppText.Get(_selectedLanguage);

            PageTitleLabel.Text = text.LanguageSelectionTitle;
            RecommendedLabel.Text = text.RecommendedLabel;
            ContinueButton.Text = text.ContinueButton;
            ChangeAnytimeLabel.Text = text.ChangeAnytimeText;
        }

        // Hàm `UpdateSelectionUi`: cập nhật dữ liệu hoặc giao diện liên quan trong file hiện tại.
        private void UpdateSelectionUi()
        {
            SetCardSelection(EnglishCard, EnglishFlag, EnglishIndicatorOuter, EnglishIndicatorInner, _selectedLanguage == AppText.English);
            SetCardSelection(VietnameseCard, VietnameseFlag, VietnameseIndicatorOuter, VietnameseIndicatorInner, _selectedLanguage == AppText.Vietnamese);
            SetCardSelection(ChineseCard, ChineseFlag, ChineseIndicatorOuter, ChineseIndicatorInner, _selectedLanguage == AppText.Chinese);
            SetCardSelection(JapaneseCard, JapaneseFlag, JapaneseIndicatorOuter, JapaneseIndicatorInner, _selectedLanguage == AppText.Japanese);
            SetCardSelection(KoreanCard, KoreanFlag, KoreanIndicatorOuter, KoreanIndicatorInner, _selectedLanguage == AppText.Korean);
        }

        // Hàm `SetCardSelection`: cập nhật giá trị hoặc trạng thái liên quan trong file hiện tại.
        private static void SetCardSelection(Border card, Label flag, Ellipse outerIndicator, Ellipse innerIndicator, bool isSelected)
        {
            if (isSelected)
            {
                card.BackgroundColor = Color.FromArgb("#FFFFFF");
                card.Stroke = Color.FromArgb("#EF8F2A");
                card.StrokeThickness = 2;
                flag.TextColor = Color.FromArgb("#EF8F2A");
                outerIndicator.Stroke = Color.FromArgb("#EF8F2A");
                outerIndicator.Fill = Color.FromArgb("#EF8F2A");
                innerIndicator.Fill = Color.FromArgb("#FFFFFF");
            }
            else
            {
                card.BackgroundColor = Color.FromArgb("#F6F1EB");
                card.Stroke = Color.FromArgb("#E5DCD3");
                card.StrokeThickness = 1;
                flag.TextColor = Color.FromArgb("#5F6877");
                outerIndicator.Stroke = Color.FromArgb("#BBC3D1");
                outerIndicator.Fill = Colors.Transparent;
                innerIndicator.Fill = Colors.Transparent;
            }
        }
    }
}
