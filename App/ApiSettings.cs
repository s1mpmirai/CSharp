namespace FoodStreetAudioGuide
{
    internal static class ApiSettings
    {
        public static string GetBaseUrl()
        {
            var overrideUrl = Environment.GetEnvironmentVariable("FOODSTREET_API_BASE_URL");
            if (!string.IsNullOrWhiteSpace(overrideUrl))
            {
                return overrideUrl.TrimEnd('/');
            }

            return DeviceInfo.Platform == DevicePlatform.Android
                ? "http://10.0.2.2:8000"
                : "http://localhost:8000";
        }
    }
}
