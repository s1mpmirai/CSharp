namespace FoodStreetAudioGuide
{
    public partial class App : Application
    {
        public static event Action? AppMovedToBackground;
        private readonly LanguageSelectionPage _startupPage;

        public App(LanguageSelectionPage startupPage)
        {
            InitializeComponent();
            _startupPage = startupPage;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new NavigationPage(_startupPage));
            window.Deactivated += OnWindowDeactivated;
            window.Stopped += OnWindowStopped;
            return window;
        }

        private static void OnWindowDeactivated(object? sender, EventArgs e)
        {
            AppMovedToBackground?.Invoke();
        }

        private static void OnWindowStopped(object? sender, EventArgs e)
        {
            AppMovedToBackground?.Invoke();
        }
    }
}
