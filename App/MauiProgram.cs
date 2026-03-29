using FoodStreetAudioGuide;
using Microsoft.Extensions.Logging;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiMaps()
            .ConfigureFonts(fonts => {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Đăng ký HttpClient kèm theo StallService luôn
        builder.Services.AddHttpClient<StallService>(client =>
        {
            // Tự động nhận diện URL theo nền tảng (Android Emulator vs Windows/Physical Device)
            string baseUrl = DeviceInfo.Platform == DevicePlatform.Android
                             ? "http://10.0.2.2:8000"
                             : "http://localhost:8000";
            client.BaseAddress = new Uri(baseUrl);
        });

        builder.Services.AddTransient<LoadingPage>();
        builder.Services.AddTransient<LanguageSelectionPage>();
        builder.Services.AddTransient<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}