using Android.App;
using Android.Content;
using Android.OS;
using System.Runtime.Versioning;
namespace FoodStreetAudioGuide;

[Service(Exported = false, ForegroundServiceType = Android.Content.PM.ForegroundService.TypeLocation)]
public sealed class BackgroundLocationTrackingService : Service
{
    private const string ChannelId = "foodstreet-location";
    private CancellationTokenSource? _cts;
    private Task? _workerTask;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        StartForeground(1001, BuildNotification());

        if (_workerTask is null || _workerTask.IsCompleted)
        {
            _cts = new CancellationTokenSource();
            _workerTask = Task.Run(() => RunAsync(_cts.Token));
        }

        return StartCommandResult.Sticky;
    }

    public override void OnDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        base.OnDestroy();
    }

    private Notification BuildNotification()
    {
        return OperatingSystem.IsAndroidVersionAtLeast(26)
            ? BuildNotificationApi26()
            : BuildNotificationLegacy();
    }

    [SupportedOSPlatform("android26.0")]
    private Notification BuildNotificationApi26()
    {
        var manager = (NotificationManager?)GetSystemService(NotificationService);
        if (manager?.GetNotificationChannel(ChannelId) is null)
        {
            var channel = new NotificationChannel(ChannelId, "FoodStreet Tracking", NotificationImportance.Low)
            {
                Description = "Background location tracking for nearby POI audio analytics"
            };
            manager?.CreateNotificationChannel(channel);
        }

        return new Notification.Builder(this, ChannelId)
            .SetContentTitle("FoodStreet dang theo doi vi tri")
            .SetContentText("Dang cap nhat POI gan ban trong nen")
            .SetSmallIcon(Resource.Mipmap.appicon)
            .SetOngoing(true)
            .Build();
    }

    [UnsupportedOSPlatform("android26.0")]
    private Notification BuildNotificationLegacy()
    {
        return new Notification.Builder(this)
            .SetContentTitle("FoodStreet dang theo doi vi tri")
            .SetContentText("Dang cap nhat POI gan ban trong nen")
            .SetSmallIcon(Resource.Mipmap.appicon)
            .SetOngoing(true)
            .Build();
    }

    private static async Task RunAsync(CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri(ApiSettings.GetBaseUrl())
        };

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var lastKnown = await Geolocation.Default.GetLastKnownLocationAsync();
                Location? location = null;
                if (lastKnown is not null &&
                    DateTimeOffset.UtcNow - lastKnown.Timestamp <= TimeSpan.FromSeconds(20) &&
                    (lastKnown.Accuracy ?? double.MaxValue) <= 20)
                {
                    location = lastKnown;
                }
                else
                {
                    var request = new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(6));
                    location = await Geolocation.Default.GetLocationAsync(request, cancellationToken);
                }

                if (location is not null)
                {
                    using var formData = new MultipartFormDataContent
                    {
                        { new StringContent(location.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)), "lat" },
                        { new StringContent(location.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)), "lng" },
                        { new StringContent("background"), "source" },
                        { new StringContent(DateTime.UtcNow.ToString("o")), "recorded_at" }
                    };
                    await httpClient.PostAsync("logs/location", formData, cancellationToken);
                }
            }
            catch
            {
                // Background tracking should stay best-effort and never crash the process.
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
            }
            catch (System.OperationCanceledException)
            {
                break;
            }
        }
    }
}
