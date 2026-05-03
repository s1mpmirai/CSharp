"""Centralized runtime configuration for the backend service."""

from __future__ import annotations

import os


# Thư mục gốc của backend. Nếu đổi cách resolve đường dẫn, web/upload có thể lỗi.
BASE_DIR = os.path.dirname(os.path.abspath(__file__))
# Chuỗi kết nối database. Đổi biến này là backend sẽ đọc/ghi sang database khác.
DATABASE_URL = os.getenv("DATABASE_URL", "postgresql://admin:password123@localhost:5432/food_street_db")
# Khóa ký token phiên. Đổi giá trị này sẽ làm cookie đăng nhập cũ hết hiệu lực.
APP_SECRET = os.getenv("APP_SECRET", "streetfeast-secret-key")
# Tên cookie phiên giữa trình duyệt và backend.
SESSION_COOKIE = "sf_session"
# Thư mục chứa HTML quản trị. Đổi sai thì web login/admin/owner sẽ không mở đúng file.
WEB_DIR = os.path.abspath(os.getenv("WEB_DIR", os.path.join(BASE_DIR, "..", "Web")))
# Thư mục lưu ảnh upload. Đổi biến này sẽ đổi nơi app/web lấy ảnh gian hàng.
UPLOAD_DIR = os.path.join(BASE_DIR, "uploads")
# Kích thước thumbnail tối đa. Tăng lên thì ảnh nét hơn nhưng tải nặng hơn trên web/app.
THUMBNAIL_MAX_SIZE = int(os.getenv("THUMBNAIL_MAX_SIZE", "160"))
# Số giờ phiên đăng nhập còn hiệu lực. Tăng thì người dùng ít phải đăng nhập lại hơn.
TOKEN_HOURS = 12
# Số vòng băm PBKDF2. Tăng thì bảo mật cao hơn nhưng xử lý đăng nhập chậm hơn.
PBKDF2_ITERATIONS = int(os.getenv("PBKDF2_ITERATIONS", "390000"))
# Chỉ gửi cookie qua HTTPS khi bật true. Nếu chạy local HTTP mà bật true thì web dễ lỗi đăng nhập.
COOKIE_SECURE = os.getenv("COOKIE_SECURE", "false").lower() == "true"
# Mật khẩu dùng khi seed tài khoản admin mặc định.
DEFAULT_ADMIN_PASSWORD = os.getenv("DEFAULT_ADMIN_PASSWORD", "admin123")
# Bật/tắt việc tự tạo admin mặc định khi bootstrap dữ liệu.
SEED_DEFAULT_ADMIN = os.getenv("SEED_DEFAULT_ADMIN", "true").lower() == "true"
# Các origin được phép gọi API từ trình duyệt. Sai giá trị có thể làm web/app bị chặn CORS.
CORS_ORIGINS = [item.strip() for item in os.getenv("CORS_ORIGINS", "*").split(",") if item.strip()]

# Danh sách ngôn ngữ backend sẽ duy trì bản dịch/script.
SUPPORTED_TRANSLATIONS = {
    "vi": "vi",
    "en": "en",
    "ko": "ko",
    "ja": "ja",
    "zh-CN": "zh-CN",
}
# Version hồ sơ audio. Đổi giá trị này sẽ khiến app/backend coi audio cache cũ là hết hạn.
AUDIO_PROFILE_VERSION = "gtts-v2"
# Số từ tối thiểu của script giới thiệu. Tăng giá trị sẽ làm form owner khó đạt điều kiện hơn.
MIN_STALL_SCRIPT_WORDS = 100
# Tên reviewer giả dùng để khôi phục rating legacy.
LEGACY_REVIEWER_NAME = "__legacy_rating_backfill__"
# Comment giả cho review legacy được tạo nhằm giữ aggregate cũ.
LEGACY_REVIEW_COMMENT = "Synthetic review row created to preserve legacy aggregate rating data."
# Cửa sổ chống spam log nghe audio. Giảm xuống sẽ làm lượt nghe được tính nhiều hơn.
LISTEN_DEDUP_WINDOW_SECONDS = 10
# Cửa sổ chống spam log quét QR. Giảm xuống sẽ làm lượt quét QR được tính nhiều hơn.
QR_DEDUP_WINDOW_SECONDS = 10
