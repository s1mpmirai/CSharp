namespace FoodStreetAudioGuide
{
    public static class AudioSettings
    {
        // Version profile audio mà app kỳ vọng từ backend.
        // Tăng giá trị này khi backend đổi cách tạo MP3 để app bỏ cache audio cũ và tải audio mới.
        public static readonly string BackendAudioProfileVersion = "gtts-v3";

        // Khi true, app chỉ phát MP3 từ backend.
        // Bật biến này sẽ tắt nhánh fallback sang TTS cục bộ nếu backend hoặc mạng bị lỗi.
        public static readonly bool UseBackendAudioOnly = false;

        // Bật Android native TTS để chỉnh tốc độ và pitch chi tiết hơn MAUI TextToSpeech.
        // Tắt biến này thì app Android sẽ dùng nhánh MAUI TTS fallback.
        public static readonly bool UseNativeAndroidTts = true;

        // Tốc độ đọc của Android native TTS.
        // Tăng giá trị sẽ làm audio đọc nhanh hơn trên Android.
        public static readonly float AndroidTtsSpeechRate = 1.0f;

        // Pitch của Android native TTS.
        // Tăng giá trị sẽ làm giọng cao hơn, giảm xuống sẽ trầm hơn.
        public static readonly float AndroidTtsPitch = 1.0f;

        // Âm lượng của nhánh MAUI TTS fallback.
        // Chỉ có tác dụng khi app không dùng MP3 backend và không dùng Android native TTS.
        public static readonly float FallbackTtsVolume = 1.0f;

        // Pitch của nhánh MAUI TTS fallback.
        // Đổi giá trị này sẽ làm giọng fallback cao/trầm hơn trên các nền tảng dùng MAUI TTS.
        public static readonly float FallbackTtsPitch = 1.0f;
    }
}
