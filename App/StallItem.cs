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
        Dictionary<string, List<string>>? SpecialtyTranslations = null,
        Dictionary<string, string>? Translations = null,
        FormattedString? HighlightedName = null,
        FormattedString? HighlightedCuisine = null,
        string ThumbnailUrl = "",
        string ImageUrl = "",
        int ReviewsCount = 0
    )
    {
        public double GetRatingValue()
        {
            return double.TryParse(Rating, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var ratingValue)
                ? ratingValue
                : 0;
        }

        public int GetReviewsCount()
        {
            if (ReviewsCount > 0)
            {
                return ReviewsCount;
            }

            var digits = new string((Reviews ?? string.Empty).Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out var parsedCount) ? parsedCount : 0;
        }

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

        public IReadOnlyList<string> GetTopSpecialties(string languageCode)
        {
            if (SpecialtyTranslations is not null && !string.IsNullOrWhiteSpace(languageCode))
            {
                if (SpecialtyTranslations.TryGetValue(languageCode, out var exactMatch) && exactMatch is { Count: > 0 })
                {
                    return exactMatch
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .Select(item => item.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(3)
                        .ToList();
                }

                var shortCode = languageCode.Split('-')[0];
                if (SpecialtyTranslations.TryGetValue(shortCode, out var shortMatch) && shortMatch is { Count: > 0 })
                {
                    return shortMatch
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .Select(item => item.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(3)
                        .ToList();
                }
            }

            if (Specialties is { Count: > 0 })
            {
                return Specialties
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(item => TranslateSpecialty(item.Trim(), languageCode))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(3)
                    .ToList();
            }

            return BuildFallbackSpecialties(languageCode);
        }

        public StallItem WithLocalizedCuisine(string languageCode)
        {
            var localizedCuisine = CategoryLocalizer.Localize(CategorySlug, languageCode, Cuisine);
            return this with { Cuisine = localizedCuisine };
        }

        private IReadOnlyList<string> BuildFallbackSpecialties(string languageCode)
        {
            var normalizedName = Name.ToLowerInvariant();
            var normalizedCuisine = Cuisine.ToLowerInvariant();

            if (normalizedName.Contains("ốc") || normalizedCuisine.Contains("hải sản"))
            {
                return new[]
                {
                    TranslateSpecialty("Ốc hương xào bơ tỏi", languageCode),
                    TranslateSpecialty("Sò điệp nướng mỡ hành", languageCode),
                    TranslateSpecialty("Càng ghẹ rang muối", languageCode)
                };
            }

            if (normalizedName.Contains("bánh tráng") || normalizedCuisine.Contains("ăn vặt"))
            {
                return new[]
                {
                    TranslateSpecialty("Bánh tráng nướng thập cẩm", languageCode),
                    TranslateSpecialty("Bánh tráng cuốn sốt me", languageCode),
                    TranslateSpecialty("Trứng cút nướng sa tế", languageCode)
                };
            }

            if (normalizedName.Contains("kem") || normalizedCuisine.Contains("tráng miệng"))
            {
                return new[]
                {
                    TranslateSpecialty("Kem dừa truyền thống", languageCode),
                    TranslateSpecialty("Thạch dừa non", languageCode),
                    TranslateSpecialty("Đậu phộng rang giòn", languageCode)
                };
            }

            if (normalizedCuisine.Contains("món việt") || normalizedCuisine.Contains("đặc sản"))
            {
                return new[]
                {
                    TranslateSpecialty("Bánh xèo tôm thịt", languageCode),
                    TranslateSpecialty("Gỏi cuốn tươi", languageCode),
                    TranslateSpecialty("Chả giò giòn rụm", languageCode)
                };
            }

            return new[]
            {
                TranslateSpecialty("Món bán chạy số 1", languageCode),
                TranslateSpecialty("Món được hỏi nhiều", languageCode),
                TranslateSpecialty("Món nên thử hôm nay", languageCode)
            };
        }

        private static string TranslateSpecialty(string value, string languageCode)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalizedCode = string.IsNullOrWhiteSpace(languageCode) ? "vi" : languageCode;
            if (KnownSpecialtyTranslations.TryGetValue(value, out var translations))
            {
                if (translations.TryGetValue(normalizedCode, out var exactMatch))
                {
                    return exactMatch;
                }

                var shortCode = normalizedCode.Split('-')[0];
                if (translations.TryGetValue(shortCode, out var shortMatch))
                {
                    return shortMatch;
                }
            }

            return value;
        }

        private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> KnownSpecialtyTranslations =
            new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["Ốc hương xào bơ tỏi"] = Build("Ốc hương xào bơ tỏi", "Wok-fried sea snails with garlic butter", "蒜香黄油炒香螺", "ニンニクバター炒めの巻き貝", "갈릭버터 소스로 볶은 소라"),
                ["Sò điệp nướng mỡ hành"] = Build("Sò điệp nướng mỡ hành", "Grilled scallops with scallion oil", "葱油烤扇贝", "ねぎ油をのせた焼きホタテ", "쪽파기름을 올린 구운 가리비"),
                ["Càng ghẹ rang muối"] = Build("Càng ghẹ rang muối", "Salt-roasted crab claws", "椒盐炒蟹钳", "塩炒めのカニの爪", "소금볶음 꽃게 집게"),
                ["Phá lấu bò"] = Build("Phá lấu bò", "Braised beef offal stew", "越式香料炖牛杂", "牛モツのスパイス煮込み", "향신료로 졸인 소 내장 요리"),
                ["Bánh mì phá lấu"] = Build("Bánh mì phá lấu", "Bread with braised beef offal", "牛杂炖汁法棍", "牛モツ煮込みのバインミー", "파러우를 넣은 바게트 샌드"),
                ["Mì phá lấu"] = Build("Mì phá lấu", "Noodles with braised beef offal", "牛杂炖汁面", "牛モツ煮込み麺", "파러우를 곁들인 국수"),
                ["Chè khúc bạch"] = Build("Chè khúc bạch", "Almond panna cotta dessert soup", "杏仁奶冻甜汤", "杏仁ミルクプリンのチェー", "아몬드 판나코타 디저트"),
                ["Chè đậu đỏ"] = Build("Chè đậu đỏ", "Sweet red bean dessert", "红豆甜品", "あずきチェー", "팥 디저트"),
                ["Sâm bổ lượng"] = Build("Sâm bổ lượng", "Herbal sweet soup", "清补凉甜汤", "漢方シロップのデザートスープ", "한방 디저트 탕"),
                ["Bánh tráng nướng thập cẩm"] = Build("Bánh tráng nướng thập cẩm", "Mixed grilled rice paper", "综合烤米纸", "具だくさん焼きライスペーパー", "모둠 구운 라이스페이퍼"),
                ["Bánh tráng cuốn sốt me"] = Build("Bánh tráng cuốn sốt me", "Rice paper rolls with tamarind sauce", "酸角酱米纸卷", "タマリンドソースのライスペーパーロール", "타마린드 소스 라이스페이퍼 롤"),
                ["Trứng cút nướng sa tế"] = Build("Trứng cút nướng sa tế", "Quail eggs grilled with satay", "沙爹烤鹌鹑蛋", "サテソースのうずら卵焼き", "사테 소스로 구운 메추리알"),
                ["Kem dừa truyền thống"] = Build("Kem dừa truyền thống", "Traditional coconut ice cream", "传统椰子冰淇淋", "伝統的なココナッツアイス", "전통 코코넛 아이스크림"),
                ["Thạch dừa non"] = Build("Thạch dừa non", "Young coconut jelly", "嫩椰果冻", "若いココナッツゼリー", "어린 코코넛 젤리"),
                ["Đậu phộng rang giòn"] = Build("Đậu phộng rang giòn", "Crunchy roasted peanuts", "香脆花生", "カリカリのローストピーナッツ", "바삭한 볶은 땅콩"),
                ["Bánh xèo tôm thịt"] = Build("Bánh xèo tôm thịt", "Vietnamese crispy pancake with shrimp and pork", "鲜虾猪肉越式煎饼", "海老と豚肉のバインセオ", "새우와 돼지고기를 넣은 반쎄오"),
                ["Gỏi cuốn tươi"] = Build("Gỏi cuốn tươi", "Fresh spring rolls", "鲜春卷", "生春巻き", "신선한 월남쌈"),
                ["Chả giò giòn rụm"] = Build("Chả giò giòn rụm", "Crispy fried spring rolls", "香脆炸春卷", "カリカリの揚げ春巻き", "바삭한 튀김 춘권"),
                ["Món bán chạy số 1"] = Build("Món bán chạy số 1", "Best-selling dish", "最受欢迎的招牌菜", "一番人気の料理", "가장 인기 있는 메뉴"),
                ["Món được hỏi nhiều"] = Build("Món được hỏi nhiều", "Most requested dish", "点单率很高的菜品", "よく注文される料理", "가장 많이 찾는 메뉴"),
                ["Món nên thử hôm nay"] = Build("Món nên thử hôm nay", "Recommended dish today", "今天推荐尝试的菜", "今日おすすめの一品", "오늘 추천 메뉴")
            };

        private static IReadOnlyDictionary<string, string> Build(string vi, string en, string zh, string ja, string ko)
            => new Dictionary<string, string>
            {
                ["vi"] = vi,
                ["en"] = en,
                ["zh-CN"] = zh,
                ["ja"] = ja,
                ["ko"] = ko
            };
    }
}
