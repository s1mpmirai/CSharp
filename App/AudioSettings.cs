namespace FoodStreetAudioGuide
{
    public static class AudioSettings
    {
        // Tăng version này mỗi khi bạn đổi cách render audio từ backend
        // để app tự bỏ cache cũ và tải lại MP3 mới.
        public static readonly string BackendAudioProfileVersion = "gtts-v2";

        // Khi true, app chỉ phát MP3 từ backend. Nếu không tải được audio thì không fallback sang TTS của máy.
        public static readonly bool UseBackendAudioOnly = false;

        // Android native TTS cho phép chỉnh tốc độ và pitch thật hơn MAUI TextToSpeech.
        public static readonly bool UseNativeAndroidTts = true;

        // 0.1f - 2.0f. 1.0f là mặc định.
        public static readonly float AndroidTtsSpeechRate = 2.0f;

        // 0.5f - 2.0f. 1.0f là mặc định.
        public static readonly float AndroidTtsPitch = 2.0f;

        // 0.0f - 1.0f. Chỉ áp dụng cho nhánh MAUI TTS fallback.
        public static readonly float FallbackTtsVolume = 1.0f;

        // 0.5f - 2.0f. Chỉ áp dụng cho nhánh MAUI TTS fallback.
        public static readonly float FallbackTtsPitch = 1.0f;
    }
}
