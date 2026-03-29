namespace FoodStreetAudioGuide
{
    public static class AppText
    {
        public const string Vietnamese = "Tiếng Việt";
        public const string English = "English";
        public const string Chinese = "中文 (Chinese)";
        public const string Japanese = "日本語 (Japanese)";
        public const string Korean = "한국어 (Korean)";

        private static readonly IReadOnlyDictionary<string, LocalizedText> TextByLanguage =
            new Dictionary<string, LocalizedText>
            {
                [Vietnamese] = new()
                {
                    PageTitle = "Danh sách quầy",
                    SearchHint = "Tìm quầy ăn, món ăn...",
                    NearbyFilter = "Gần đây",
                    OpenNowFilter = "Đang mở",
                    TopRatedFilter = "Đánh giá cao",
                    Under200Filter = "Dưới 200m",
                    ExploreTab = "KHÁM PHÁ",
                    MapTab = "BẢN ĐỒ",
                    SavedTab = "ĐÃ LƯU",
                    AwayLabel = "KHOẢNG CÁCH",
                    UnavailableContentMessage = "Nội dung chưa sẵn sàng. Đổi ngôn ngữ?",
                    CancelText = "Hủy",
                    ErrorTitle = "Lỗi",
                    AudioErrorPrefix = "Không thể phát âm thanh: ",
                    LanguageSelectionTitle = "Chọn ngôn ngữ của bạn",
                    RecommendedLabel = "Đề xuất",
                    ContinueButton = "Tiếp tục",
                    ChangeAnytimeText = "Bạn có thể thay đổi bất cứ lúc nào"
                },
                [English] = new()
                {
                    PageTitle = "Stall List",
                    SearchHint = "Search food stalls, cuisines...",
                    NearbyFilter = "Nearby",
                    OpenNowFilter = "Open Now",
                    TopRatedFilter = "Top Rated",
                    Under200Filter = "Under 200m",
                    ExploreTab = "EXPLORES",
                    MapTab = "MAP",
                    SavedTab = "SAVED",
                    AwayLabel = "AWAY",
                    UnavailableContentMessage = "Content is not ready yet. Change language?",
                    CancelText = "Cancel",
                    ErrorTitle = "Error",
                    AudioErrorPrefix = "Unable to play audio: ",
                    LanguageSelectionTitle = "Select Your Language",
                    RecommendedLabel = "Recommended",
                    ContinueButton = "Continue",
                    ChangeAnytimeText = "You can change this anytime"
                },
                [Chinese] = new()
                {
                    PageTitle = "摊位列表",
                    SearchHint = "搜索美食摊位...",
                    NearbyFilter = "附近",
                    OpenNowFilter = "营业中",
                    TopRatedFilter = "高评分",
                    Under200Filter = "200米内",
                    ExploreTab = "探索",
                    MapTab = "地图",
                    SavedTab = "收藏",
                    AwayLabel = "距离",
                    UnavailableContentMessage = "内容尚未准备好。要切换语言吗？",
                    CancelText = "取消",
                    ErrorTitle = "错误",
                    AudioErrorPrefix = "无法播放音频：",
                    LanguageSelectionTitle = "选择您的语言",
                    RecommendedLabel = "推荐",
                    ContinueButton = "继续",
                    ChangeAnytimeText = "您可以随时更改"
                },
                [Japanese] = new()
                {
                    PageTitle = "屋台一覧",
                    SearchHint = "屋台や料理を検索...",
                    NearbyFilter = "近く",
                    OpenNowFilter = "営業中",
                    TopRatedFilter = "高評価",
                    Under200Filter = "200m以内",
                    ExploreTab = "探索",
                    MapTab = "地図",
                    SavedTab = "保存済み",
                    AwayLabel = "距離",
                    UnavailableContentMessage = "コンテンツの準備ができていません。言語を変更しますか？",
                    CancelText = "キャンセル",
                    ErrorTitle = "エラー",
                    AudioErrorPrefix = "音声を再生できません: ",
                    LanguageSelectionTitle = "言語を選択",
                    RecommendedLabel = "おすすめ",
                    ContinueButton = "続行",
                    ChangeAnytimeText = "この設定はいつでも変更できます"
                },
                [Korean] = new()
                {
                    PageTitle = "가판대 목록",
                    SearchHint = "음식 가판대, 요리를 검색...",
                    NearbyFilter = "근처",
                    OpenNowFilter = "영업 중",
                    TopRatedFilter = "평점 높음",
                    Under200Filter = "200m 이내",
                    ExploreTab = "탐색",
                    MapTab = "지도",
                    SavedTab = "저장됨",
                    AwayLabel = "거리",
                    UnavailableContentMessage = "콘텐츠가 아직 준비되지 않았습니다. 언어를 변경할까요?",
                    CancelText = "취소",
                    ErrorTitle = "오류",
                    AudioErrorPrefix = "오디오를 재생할 수 없습니다: ",
                    LanguageSelectionTitle = "언어 선택",
                    RecommendedLabel = "추천",
                    ContinueButton = "계속",
                    ChangeAnytimeText = "이 설정은 언제든지 변경할 수 있습니다"
                }
            };

        public static LocalizedText Get(string? language)
        {
            if (!string.IsNullOrWhiteSpace(language) && TextByLanguage.TryGetValue(language, out var text))
            {
                return text;
            }

            return TextByLanguage[English];
        }
    }
}
