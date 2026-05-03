namespace FoodStreetAudioGuide
{
    internal static class ApiSettings
    {
        private const string DefaultLanBaseUrl = "https://hoafoodaudio.live";

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
