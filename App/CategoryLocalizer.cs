namespace FoodStreetAudioGuide
{
    internal static class CategoryLocalizer
    {
        private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> NamesBySlug =
            new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["seafood"] = Build("Hải sản", "Seafood", "海鲜", "シーフード", "해산물"),
                ["grilled"] = Build("Đồ nướng", "Grilled", "烧烤", "焼き物", "구이"),
                ["noodles"] = Build("Món nước", "Noodles", "汤面", "麺料理", "면 요리"),
                ["snacks"] = Build("Ăn vặt", "Snacks", "小吃", "軽食", "간식"),
                ["desserts"] = Build("Tráng miệng", "Desserts", "甜品", "デザート", "디저트"),
                ["rice"] = Build("Cơm", "Rice", "米饭", "ご飯もの", "밥류"),
                ["dumplings"] = Build("Há cảo", "Dumplings", "点心", "点心", "딤섬"),
                ["specialties"] = Build("Đặc sản", "Specialties", "特色美食", "名物料理", "특산 요리"),
            };

        public static string Localize(string? slug, string? languageCode, string fallback = "")
        {
            if (string.IsNullOrWhiteSpace(slug) || !NamesBySlug.TryGetValue(slug, out var names))
            {
                return fallback;
            }

            var key = NormalizeLanguageCode(languageCode);
            if (names.TryGetValue(key, out var localized))
            {
                return localized;
            }

            return names["vi"];
        }

        private static IReadOnlyDictionary<string, string> Build(string vi, string en, string zhCn, string ja, string ko)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["vi"] = vi,
                ["en"] = en,
                ["zh-CN"] = zhCn,
                ["ja"] = ja,
                ["ko"] = ko
            };
        }

        private static string NormalizeLanguageCode(string? languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode))
            {
                return "vi";
            }

            return languageCode.Equals("zh", StringComparison.OrdinalIgnoreCase)
                ? "zh-CN"
                : languageCode;
        }
    }
}
