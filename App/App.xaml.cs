namespace FoodStreetAudioGuide
{
    public partial class App : Application
    {
        private readonly LoadingPage _startupPage;

        // Constructor nhận LoadingPage từ hệ thống DI. 
        // MAUI sẽ tự biết LoadingPage cần StallService và tự chuẩn bị cho bạn.
        public App(LoadingPage startupPage)
        {
            InitializeComponent();
            _startupPage = startupPage;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            // Sử dụng trang startupPage đã được "tiêm" sẵn Service từ trước
            return new Window(new NavigationPage(_startupPage));
        }
    }
}