namespace FoodStreetAudioGuide.Models
{
    public record StallItem(
        int Id,
        string DistanceText,
        string Name,
        string Rating,
        string Reviews,
        string Cuisine,
        string CategorySlug = "",
        string OpeningHours = "",
        string OpeningTime = "",
        string ClosingTime = "",
        double Distance = 0,
        double Lat = 0,
        double Lng = 0,
        double PoiRadiusMeters = 0,
        bool HasOfflineAudio = false,
        List<string>? Specialties = null,
        Dictionary<string, string>? Translations = null,
        FormattedString? HighlightedName = null,
        FormattedString? HighlightedCuisine = null,
        string ThumbnailUrl = "",
        string ImageUrl = ""
    )
    {
        public string GetDisplayHours()
        {
            if (!string.IsNullOrWhiteSpace(OpeningHours))
            {
                return OpeningHours;
            }

            var values = new[] { OpeningTime, ClosingTime }
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray();

            return values.Length == 2 ? $"{values[0]} - {values[1]}" : string.Empty;
        }

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

        public IReadOnlyList<string> GetTopSpecialties()
        {
            if (Specialties is { Count: > 0 })
            {
                return Specialties
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(item => item.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(3)
                    .ToList();
            }

            return BuildFallbackSpecialties();
        }

        public StallItem WithLocalizedCuisine(string languageCode)
        {
            var localizedCuisine = CategoryLocalizer.Localize(CategorySlug, languageCode, Cuisine);
            return this with { Cuisine = localizedCuisine };
        }

        private IReadOnlyList<string> BuildFallbackSpecialties()
        {
            var normalizedName = Name.ToLowerInvariant();
            var normalizedCuisine = Cuisine.ToLowerInvariant();

            if (normalizedName.Contains("ốc") || normalizedCuisine.Contains("hải sản"))
            {
                return new[] { "Ốc hương xào bơ tỏi", "Sò điệp nướng mỡ hành", "Càng ghẹ rang muối" };
            }

            if (normalizedName.Contains("bánh tráng") || normalizedCuisine.Contains("ăn vặt"))
            {
                return new[] { "Bánh tráng nướng thập cẩm", "Bánh tráng cuốn sốt me", "Trứng cút nướng sa tế" };
            }

            if (normalizedName.Contains("kem") || normalizedCuisine.Contains("tráng miệng"))
            {
                return new[] { "Kem dừa truyền thống", "Thạch dừa non", "Đậu phộng rang giòn" };
            }

            if (normalizedCuisine.Contains("món việt") || normalizedCuisine.Contains("đặc sản"))
            {
                return new[] { "Bánh xèo tôm thịt", "Gỏi cuốn tươi", "Chả giò giòn rụm" };
            }

            return new[] { "Món bán chạy số 1", "Món được hỏi nhiều", "Món nên thử hôm nay" };
        }
    }
}
