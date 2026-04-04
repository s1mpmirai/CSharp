namespace FoodStreetAudioGuide
{
    public partial class App : Application
    {
        private readonly LanguageSelectionPage _startupPage;

        public App(LanguageSelectionPage startupPage)
        {
            InitializeComponent();
            _startupPage = startupPage;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new NavigationPage(_startupPage));
        }
    }
}
