using CommunityToolkit.Maui;
using FoodStreetAudioGuide;
using Microsoft.Extensions.Logging;
using ZXing.Net.Maui.Controls;

public static class MauiProgram
{
    private static readonly TimeSpan DefaultApiTimeout = TimeSpan.FromSeconds(12);

    // Hàm `CreateMauiApp`: tạo dữ liệu hoặc đối tượng cần dùng trong file hiện tại.
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseBarcodeReader()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton<OfflineCacheService>();

        builder.Services.AddHttpClient<StallService>(client =>
        {
            client.BaseAddress = new Uri(ApiSettings.GetBaseUrl());
            client.Timeout = DefaultApiTimeout;
        });

        builder.Services.AddHttpClient<AudioCacheService>(client =>
        {
            client.BaseAddress = new Uri(ApiSettings.GetBaseUrl());
            client.Timeout = DefaultApiTimeout;
        });

        builder.Services.AddTransient<LanguageSelectionPage>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<DownloadedAudioPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
