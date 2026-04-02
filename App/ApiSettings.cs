namespace FoodStreetAudioGuide
{
    internal static class ApiSettings
    {
        private const string DefaultLanBaseUrl = "http://192.168.1.165:8000";

        public static string GetBaseUrl()
        {
            var overrideUrl = Environment.GetEnvironmentVariable("FOODSTREET_API_BASE_URL");
            if (!string.IsNullOrWhiteSpace(overrideUrl))
            {
                return overrideUrl.TrimEnd('/');
            }

            return DefaultLanBaseUrl;
        }
    }
}
