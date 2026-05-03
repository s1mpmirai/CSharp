namespace FoodStreetAudioGuide
{
    public partial class App : Application
    {
        public static event Action? AppMovedToBackground;
        private readonly LanguageSelectionPage _startupPage;

        // Hàm khởi tạo `App`: thiết lập trạng thái ban đầu cho đối tượng trong file hiện tại.
        public App(LanguageSelectionPage startupPage)
        {
            InitializeComponent();
            _startupPage = startupPage;
        }

        // Hàm `CreateWindow`: tạo dữ liệu hoặc đối tượng cần dùng trong file hiện tại.
        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new NavigationPage(_startupPage));
            window.Deactivated += OnWindowDeactivated;
            window.Stopped += OnWindowStopped;
            return window;
        }

        // Hàm `OnWindowDeactivated`: xử lý sự kiện hoặc callback liên quan trong file hiện tại.
        private static void OnWindowDeactivated(object? sender, EventArgs e)
        {
            AppMovedToBackground?.Invoke();
        }

        // Hàm `OnWindowStopped`: xử lý sự kiện hoặc callback liên quan trong file hiện tại.
        private static void OnWindowStopped(object? sender, EventArgs e)
        {
            AppMovedToBackground?.Invoke();
        }
    }
}
