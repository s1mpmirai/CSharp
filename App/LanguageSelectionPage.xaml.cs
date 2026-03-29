using Microsoft.Maui.Controls.Shapes;

namespace FoodStreetAudioGuide
{
    public partial class LanguageSelectionPage : ContentPage
    {
        private const string SelectedLanguagePreferenceKey = "SelectedLanguage";

        private readonly StallService _stallService;
        private string _selectedLanguage;

        public LanguageSelectionPage(StallService stallService)
        {
            InitializeComponent();

            _stallService = stallService;
            _selectedLanguage = Preferences.Get(SelectedLanguagePreferenceKey, AppText.English);

            ApplyPageText();
            UpdateSelectionUi();
        }

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

        private void OnContinueClicked(object sender, EventArgs e)
        {
            Preferences.Set(SelectedLanguagePreferenceKey, _selectedLanguage);

            var window = Application.Current?.Windows.FirstOrDefault();
            if (window is not null)
            {
                window.Page = new NavigationPage(new MainPage(_stallService, _selectedLanguage));
            }
        }

        private void ApplyPageText()
        {
            var text = AppText.Get(_selectedLanguage);

            PageTitleLabel.Text = text.LanguageSelectionTitle;
            RecommendedLabel.Text = text.RecommendedLabel;
            ContinueButton.Text = text.ContinueButton;
            ChangeAnytimeLabel.Text = text.ChangeAnytimeText;
        }

        private void UpdateSelectionUi()
        {
            SetCardSelection(EnglishCard, EnglishFlag, EnglishIndicatorOuter, EnglishIndicatorInner, _selectedLanguage == AppText.English);
            SetCardSelection(VietnameseCard, VietnameseFlag, VietnameseIndicatorOuter, VietnameseIndicatorInner, _selectedLanguage == AppText.Vietnamese);
            SetCardSelection(ChineseCard, ChineseFlag, ChineseIndicatorOuter, ChineseIndicatorInner, _selectedLanguage == AppText.Chinese);
            SetCardSelection(JapaneseCard, JapaneseFlag, JapaneseIndicatorOuter, JapaneseIndicatorInner, _selectedLanguage == AppText.Japanese);
            SetCardSelection(KoreanCard, KoreanFlag, KoreanIndicatorOuter, KoreanIndicatorInner, _selectedLanguage == AppText.Korean);
        }

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
                card.BackgroundColor = Color.FromArgb("#F1F4F8");
                card.Stroke = Color.FromArgb("#D9DEE8");
                card.StrokeThickness = 1;
                flag.TextColor = Color.FromArgb("#5F6877");
                outerIndicator.Stroke = Color.FromArgb("#BBC3D1");
                outerIndicator.Fill = Colors.Transparent;
                innerIndicator.Fill = Colors.Transparent;
            }
        }
    }
}
