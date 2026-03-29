namespace FoodStreetAudioGuide.Models
{
    public record StallItem(
        string DistanceText,
        string Name,
        string Rating,
        string Reviews,
        string Cuisine,
        Dictionary<string, string>? Translations = null,
        string ImageUrl = ""
    )
    {
        public string GetScript(string languageCode)
        {
            if (Translations is null || string.IsNullOrWhiteSpace(languageCode))
            {
                return string.Empty;
            }

            if (Translations.TryGetValue(languageCode, out var exactMatch) && !string.IsNullOrWhiteSpace(exactMatch))
            {
                return exactMatch;
            }

            var shortCode = languageCode.Split('-')[0];
            if (Translations.TryGetValue(shortCode, out var shortMatch) && !string.IsNullOrWhiteSpace(shortMatch))
            {
                return shortMatch;
            }

            return string.Empty;
        }
    }
}
