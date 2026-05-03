using Android.App;
using Android.Runtime;

namespace FoodStreetAudioGuide
{
    [Application]
    public class MainApplication : MauiApplication
    {
        // Hàm khởi tạo `MainApplication`: thiết lập trạng thái ban đầu cho đối tượng trong file hiện tại.
        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
