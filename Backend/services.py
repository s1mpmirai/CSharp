"""Shared backend services and domain helpers.

Logic in this file is copied from the previous monolithic `main.py` so the
refactor only changes file boundaries, not behavior.
"""

from __future__ import annotations

from fastapi import HTTPException, Request
from fastapi.responses import FileResponse, Response
from sqlalchemy import func, or_
from sqlalchemy.orm import Session, joinedload
from deep_translator import GoogleTranslator
from gtts import gTTS
from datetime import datetime, timedelta
from typing import Optional
from io import BytesIO
from PIL import Image
import os
import shutil
import uuid
import hashlib
import hmac
import base64
import json
import secrets
import unicodedata
import re
import math

from Backend.config import (
    APP_SECRET,
    AUDIO_PROFILE_VERSION,
    DEFAULT_ADMIN_PASSWORD,
    LEGACY_REVIEW_COMMENT,
    LEGACY_REVIEWER_NAME,
    MIN_STALL_SCRIPT_WORDS,
    PBKDF2_ITERATIONS,
    QR_DEDUP_WINDOW_SECONDS,
    SEED_DEFAULT_ADMIN,
    SESSION_COOKIE,
    SUPPORTED_TRANSLATIONS,
    THUMBNAIL_MAX_SIZE,
    TOKEN_HOURS,
    UPLOAD_DIR,
    WEB_DIR,
)
from Backend.db import SessionLocal
from Backend.models import (
    AdminNotification,
    AdminNotificationRecipient,
    Category,
    Language,
    ListeningLog,
    LocationLog,
    QrScanLog,
    Review,
    Role,
    Stall,
    StallAudioAsset,
    StallTranslation,
    StallUpdateRequest,
    User,
)

# Trả về file giao diện web và ép trình duyệt không dùng cache cũ.
def web_file_response(filename: str) -> FileResponse:
    response = FileResponse(os.path.join(WEB_DIR, filename))
    response.headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0"
    response.headers["Pragma"] = "no-cache"
    response.headers["Expires"] = "0"
    return response


# Ghép URL tuyệt đối để client truy cập ảnh đã upload.
def build_upload_url(request: Request, filename: str) -> str:
    return str(request.base_url).rstrip("/") + f"/uploads/{filename}"


# Ghép URL tuyệt đối để client truy cập thumbnail của ảnh upload.
def build_thumbnail_url(request: Request, filename: str) -> str:
    return str(request.base_url).rstrip("/") + f"/thumbnails/{filename}"

# Băm mật khẩu bằng PBKDF2 để lưu an toàn trong database.
def hash_password(password: str) -> str:
    salt = secrets.token_hex(16)
    digest = hashlib.pbkdf2_hmac("sha256", password.encode("utf-8"), salt.encode("utf-8"), PBKDF2_ITERATIONS)
    encoded = base64.urlsafe_b64encode(digest).decode("utf-8").rstrip("=")
    return f"pbkdf2_sha256${PBKDF2_ITERATIONS}${salt}${encoded}"


# Kiểm tra mật khẩu đầu vào với hash mới hoặc hash legacy cũ.
def verify_password(password: str, password_hash: str) -> bool:
    if password_hash.startswith("pbkdf2_sha256$"):
        try:
            _, iterations, salt, encoded_hash = password_hash.split("$", 3)
            digest = hashlib.pbkdf2_hmac(
                "sha256",
                password.encode("utf-8"),
                salt.encode("utf-8"),
                int(iterations)
            )
            expected = base64.urlsafe_b64encode(digest).decode("utf-8").rstrip("=")
            return hmac.compare_digest(expected, encoded_hash)
        except (TypeError, ValueError):
            return False

    legacy_hash = hashlib.sha256(password.encode("utf-8")).hexdigest()
    return hmac.compare_digest(legacy_hash, password_hash)


# Xác định hash hiện tại có cần nâng cấp sang chuẩn mới hay không.
def needs_password_rehash(password_hash: str) -> bool:
    return not password_hash.startswith("pbkdf2_sha256$")


# Mã hóa payload phiên đăng nhập thành token có chữ ký HMAC.
def encode_token(data: dict) -> str:
    payload = base64.urlsafe_b64encode(json.dumps(data).encode("utf-8")).decode("utf-8").rstrip("=")
    signature = hmac.new(APP_SECRET.encode("utf-8"), payload.encode("utf-8"), hashlib.sha256).hexdigest()
    return f"{payload}.{signature}"


# Tạo giá trị cookie phiên đăng nhập cho người dùng hiện tại.
def create_session_cookie(user: User) -> str:
    return encode_token({
        "uid": user.id,
        "role": user.role.name,
        "exp": int((datetime.utcnow() + timedelta(hours=TOKEN_HOURS)).timestamp())
    })


# Lấy người dùng hiện tại từ cookie phiên nếu token hợp lệ.
def get_current_user_from_request(request: Request, db: Session) -> Optional[User]:
    token = request.cookies.get(SESSION_COOKIE)
    if not token:
        return None
    try:
        data = decode_token(token)
    except Exception:
        return None
    return db.query(User).options(joinedload(User.role)).filter(User.id == data["uid"], User.is_active == True).first()


# Dịch văn bản tiếng Việt sang ngôn ngữ đích bằng dịch vụ ngoài.
def translate_text(text: str, lang_code: str) -> str:
    try:
        return GoogleTranslator(source="vi", target=lang_code).translate(text)
    except Exception:
        return ""


# Tạo map ngôn ngữ đang hoạt động theo mã code để tra cứu nhanh.
def get_language_map(db: Session) -> dict[str, Language]:
    return {item.code: item for item in db.query(Language).filter(Language.is_active == True).all()}


# Bổ sung URL tuyệt đối và QR code vào payload gian hàng của chủ quán.
def serialize_owner_stall_for_request(request: Request, stall: Stall) -> dict:
    payload = serialize_owner_stall(stall)
    if stall.image_url:
        payload["image_url"] = str(request.base_url).rstrip("/") + f"/uploads/{stall.image_url}"
    payload["qr_code_value"] = build_stall_qr_code(stall.id)
    payload["qr_launch_url"] = str(request.base_url).rstrip("/") + f"/qr/resolve?code={payload['qr_code_value']}"
    return payload


# Giải mã token phiên đăng nhập và kiểm tra chữ ký cùng thời hạn.
def decode_token(token: str) -> dict:
    payload, signature = token.split('.', 1)
    expected = hmac.new(APP_SECRET.encode('utf-8'), payload.encode('utf-8'), hashlib.sha256).hexdigest()
    if not hmac.compare_digest(signature, expected):
        raise HTTPException(status_code=401, detail='Phiên đăng nhập không hợp lệ')
    padding = '=' * (-len(payload) % 4)
    data = json.loads(base64.urlsafe_b64decode((payload + padding).encode('utf-8')).decode('utf-8'))
    if data.get('exp', 0) < int(datetime.utcnow().timestamp()):
        raise HTTPException(status_code=401, detail='Phiên đăng nhập đã hết hạn')
    return data


# Tạo mã QR ký số cho một gian hàng để chống sửa mã thủ công.
def build_stall_qr_code(stall_id: int) -> str:
    payload = f"stall:{stall_id}"
    signature = hmac.new(APP_SECRET.encode("utf-8"), payload.encode("utf-8"), hashlib.sha256).hexdigest()[:16]
    return f"sfqr1.{stall_id}.{signature}"


# Giải mã và kiểm tra tính hợp lệ của mã QR gian hàng.
def resolve_stall_qr_code(code: str) -> int:
    parts = (code or "").strip().split(".")
    if len(parts) != 3 or parts[0] != "sfqr1":
        raise HTTPException(status_code=400, detail="Mã QR không hợp lệ")

    _, stall_id_text, signature = parts
    try:
        stall_id = int(stall_id_text)
    except ValueError as ex:
        raise HTTPException(status_code=400, detail="Mã QR không hợp lệ") from ex

    expected = hmac.new(APP_SECRET.encode("utf-8"), f"stall:{stall_id}".encode("utf-8"), hashlib.sha256).hexdigest()[:16]
    if not hmac.compare_digest(signature, expected):
        raise HTTPException(status_code=400, detail="Mã QR không hợp lệ")

    return stall_id


# Bắt buộc request phải đăng nhập, nếu không sẽ trả về lỗi 401.
def require_auth_page(request: Request, db: Session) -> User:
    user = get_current_user_from_request(request, db)
    if not user:
        raise HTTPException(status_code=401, detail='Bạn cần đăng nhập')
    return user


# Kiểm tra người dùng có đúng vai trò được yêu cầu hay không.
def require_role(user: User, role_name: str):
    if not user.role or user.role.name != role_name:
        raise HTTPException(status_code=403, detail='Không có quyền truy cập')


# Chuẩn hóa email đầu vào và kiểm tra định dạng hợp lệ.
def normalize_email_input(value: Optional[str], *, required: bool = False) -> Optional[str]:
    normalized = (value or "").strip().lower()
    if not normalized:
        if required:
            raise HTTPException(status_code=400, detail="Vui lòng nhập email")
        return None

    if not re.fullmatch(r"^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$", normalized):
        raise HTTPException(status_code=400, detail="Email không đúng định dạng")
    return normalized


# Làm sạch mật khẩu đầu vào và kiểm tra độ dài tối thiểu.
def validate_password_input(password: Optional[str], *, required: bool = False) -> Optional[str]:
    normalized = (password or "").strip()
    if not normalized:
        if required:
            raise HTTPException(status_code=400, detail="Vui lòng nhập mật khẩu")
        return None

    if len(normalized) < 8:
        raise HTTPException(status_code=400, detail="Mật khẩu phải có ít nhất 8 ký tự")
    return normalized


# Đếm số từ trong một đoạn văn bản Unicode.
def count_words(text: Optional[str]) -> int:
    return len(re.findall(r"\b\w+\b", text or "", flags=re.UNICODE))


# Bắt buộc script mô tả gian hàng phải đủ số từ tối thiểu.
def require_minimum_stall_script(value: Optional[str]) -> str:
    normalized = re.sub(r"\s+", " ", (value or "").strip())
    if not normalized:
        raise HTTPException(status_code=400, detail="Vui lòng nhập script tiếng Việt")
    if count_words(normalized) < MIN_STALL_SCRIPT_WORDS:
        raise HTTPException(
            status_code=400,
            detail=f"Script giới thiệu quán ăn phải có ít nhất {MIN_STALL_SCRIPT_WORDS} chữ"
        )
    return normalized


# Xác định trang đích mặc định theo vai trò người dùng.
def get_home_redirect_for_user(user: Optional[User]) -> str:
    if not user or not user.role:
        return "/login"
    if user.role.name == "super_admin":
        return "/superadmin"
    return "/owner"


# Tạo bộ bản dịch tiêu đề và script từ nội dung tiếng Việt gốc.
def build_translations(base_title: str, base_script_vi: str) -> dict[str, dict[str, str]]:
    results = {}
    for language_code, target_code in SUPPORTED_TRANSLATIONS.items():
        if language_code == "vi":
            results[language_code] = {"title": base_title, "script_text": base_script_vi}
        else:
            results[language_code] = {
                "title": translate_text(base_title, target_code) if base_title else "",
                "script_text": translate_text(base_script_vi, target_code) if base_script_vi else ""
            }
    return results


# Chuẩn hóa dữ liệu món đặc sản trả về cho client mà không dịch runtime.
def build_specialty_translations(specialties: list[str]) -> dict[str, list[str]]:
    clean_specialties = [item.strip() for item in specialties if item and item.strip()]
    if not clean_specialties:
        return {}

    return {"vi": clean_specialties}


# Thêm mới hoặc cập nhật toàn bộ bản dịch cho một gian hàng.
def upsert_stall_translations(db: Session, stall: Stall, base_title: str, base_script_vi: str):
    language_map = get_language_map(db)
    existing = {item.language_id: item for item in stall.translations}
    for code, content in build_translations(base_title, base_script_vi).items():
        language = language_map.get(code)
        if not language or not content["script_text"]:
            continue
        row = existing.get(language.id)
        if row:
            row.title = content["title"]
            row.script_text = content["script_text"]
            row.description = None
            row.updated_at = datetime.utcnow()
            row.is_auto_generated = code != "vi"
            row.translation_status = "approved" if code == "vi" else "auto_generated"
            row.source_version = row.source_version + 1
        else:
            db.add(StallTranslation(
                stall_id=stall.id,
                language_id=language.id,
                title=content["title"],
                description=None,
                script_text=content["script_text"],
                is_auto_generated=(code != "vi"),
                translation_status="approved" if code == "vi" else "auto_generated",
                source_version=1
            ))


# Lấy gian hàng đang hoạt động của một chủ gian hàng.
def get_owner_stall(db: Session, user_id: int) -> Optional[Stall]:
    return (
        db.query(Stall)
        .options(joinedload(Stall.category), joinedload(Stall.translations).joinedload(StallTranslation.language))
        .filter(
            Stall.created_by_user_id == user_id,
            Stall.is_deleted == False,
            Stall.is_active == True
        )
        .order_by(Stall.id.desc())
        .first()
    )


# Lấy gian hàng gần nhất của chủ quán kể cả khi chưa được kích hoạt.
def get_owner_any_stall(db: Session, user_id: int) -> Optional[Stall]:
    return (
        db.query(Stall)
        .options(joinedload(Stall.category), joinedload(Stall.translations).joinedload(StallTranslation.language))
        .filter(Stall.created_by_user_id == user_id, Stall.is_deleted == False)
        .order_by(Stall.id.desc())
        .first()
    )


# Lấy yêu cầu cập nhật đang chờ duyệt mới nhất của chủ gian hàng.
def get_owner_pending_request(db: Session, user_id: int) -> Optional[StallUpdateRequest]:
    return (
        db.query(StallUpdateRequest)
        .join(Stall, Stall.id == StallUpdateRequest.stall_id)
        .filter(
            Stall.created_by_user_id == user_id,
            Stall.is_deleted == False,
            StallUpdateRequest.status == "pending"
        )
        .order_by(StallUpdateRequest.submitted_at.desc(), StallUpdateRequest.id.desc())
        .first()
    )


# Kiểm tra chủ quán có đang chờ duyệt gian hàng đầu tiên hay không.
def owner_is_waiting_for_initial_approval(db: Session, user_id: int) -> bool:
    stall = get_owner_any_stall(db, user_id)
    if not stall or stall.is_active:
        return False
    pending = get_owner_pending_request(db, user_id)
    return pending is not None


# Chuyển danh sách bản dịch của gian hàng thành dict theo mã ngôn ngữ.
def translations_to_dict(stall: Stall) -> dict[str, str]:
    output = {}
    for item in stall.translations:
        if item.language and item.script_text:
            output[item.language.code] = item.script_text
    return output


# Làm sạch tối đa 3 món đặc sản đầu vào và loại bỏ giá trị rỗng.
def normalize_specialty_values(*values: Optional[str]) -> list[str]:
    items = []
    for value in values:
        cleaned = (value or "").strip()
        if cleaned:
            items.append(cleaned)
    return items[:3]


# Bắt buộc biểu mẫu phải có đúng 3 món đặc sản hợp lệ.
def require_specialties(*values: Optional[str]) -> tuple[str, str, str]:
    items = normalize_specialty_values(*values)
    if len(items) != 3:
        raise HTTPException(status_code=400, detail="Vui lòng nhập đủ 3 món đặc sản")
    return items[0], items[1], items[2]


# Ghép danh sách món đặc sản thành câu tiếng Việt dễ đọc.
def format_specialties_text(specialties: list[str]) -> str:
    items = [item.strip() for item in specialties if item and item.strip()]
    if not items:
        return "những món đặc trưng của quán"
    if len(items) == 1:
        return items[0]
    if len(items) == 2:
        return f"{items[0]} và {items[1]}"
    return f"{', '.join(items[:-1])} và {items[-1]}"


# Tạo đoạn giới thiệu dài mặc định cho gian hàng khi script còn thiếu.
def build_verbose_stall_intro(
    stall_name: str,
    category_name: str,
    specialties: list[str],
    opening_hours: str,
    poi_radius_m: float
) -> str:
    clean_name = (stall_name or "Gian hàng này").strip() or "Gian hàng này"
    clean_category = (category_name or "ẩm thực địa phương").strip() or "ẩm thực địa phương"
    specialties_text = format_specialties_text(specialties)
    opening_text = (opening_hours or "").strip() or "khung giờ phục vụ được cập nhật tại quầy"
    radius_text = int(round(float(poi_radius_m or 30)))

    text = (
        f"{clean_name} là một điểm dừng chân đáng chú ý dành cho thực khách muốn khám phá {clean_category} "
        f"trong không gian gần gũi, dễ tiếp cận và phù hợp cho cả khách đi lần đầu lẫn khách quen trong khu vực. "
        f"Khi ghé quán, bạn có thể ưu tiên trải nghiệm các món nổi bật như {specialties_text}, bởi đây là những lựa "
        f"chọn thể hiện rõ hương vị đặc trưng, cách nêm nếm riêng và sự chỉn chu của gian hàng trong từng phần ăn. "
        f"Điểm đáng chú ý của quán nằm ở cách kết hợp nguyên liệu quen thuộc với cách chế biến ổn định, giúp món ăn "
        f"giữ được độ nóng, mùi thơm và cảm giác tròn vị ngay cả vào những khung giờ đông khách. Bên cạnh chất lượng "
        f"món chính, quán còn tạo thiện cảm nhờ nhịp phục vụ linh hoạt, không khí thân thiện và trải nghiệm dùng bữa "
        f"phù hợp cho bữa sáng, bữa trưa hoặc một lần ghé nhanh để thưởng thức đặc sản trong ngày. Gian hàng hiện mở "
        f"cửa theo khung giờ {opening_text}, đồng thời khu vực kích hoạt audio guide được thiết lập khoảng {radius_text} mét "
        f"để khách dễ nhận nội dung thuyết minh khi đến gần. Nếu bạn muốn hiểu nhanh về phong cách món ăn, khẩu vị gợi ý "
        f"và những lựa chọn nên thử trước, phần giới thiệu này sẽ giúp bạn có hình dung rõ ràng hơn trước khi gọi món."
    )
    return re.sub(r"\s+", " ", text).strip()


# Kiểm tra bán kính POI có đạt ngưỡng tối thiểu cho audio guide hay không.
def require_poi_radius(value: Optional[float]) -> float:
    radius = float(value or 0)
    if radius < 10:
        raise HTTPException(status_code=400, detail="Vui lòng nhập bán kính POI tối thiểu 10m")
    return radius


# Chuẩn hóa một mốc giờ theo định dạng HH:MM.
def normalize_time_value(value: Optional[str]) -> str:
    cleaned = (value or "").strip()
    if not cleaned:
        return ""
    if not re.fullmatch(r"\d{2}:\d{2}", cleaned):
        raise HTTPException(status_code=400, detail="Vui lòng nhập giờ theo định dạng HH:MM")
    return cleaned


# Ghép giờ mở và giờ đóng thành chuỗi hiển thị thống nhất.
def build_opening_hours(opening_time: Optional[str], closing_time: Optional[str]) -> str:
    open_value = normalize_time_value(opening_time)
    close_value = normalize_time_value(closing_time)
    if not open_value or not close_value:
        raise HTTPException(status_code=400, detail="Vui lòng nhập đầy đủ giờ mở và giờ đóng")
    return f"{open_value} - {close_value}"


# Ưu tiên lấy giờ mở/đóng tách riêng, nếu thiếu thì phân tích từ chuỗi cũ.
def resolve_opening_hours_input(
    opening_time: Optional[str],
    closing_time: Optional[str],
    opening_hours: Optional[str]
) -> str:
    has_explicit_times = bool((opening_time or "").strip() or (closing_time or "").strip())
    if has_explicit_times:
        return build_opening_hours(opening_time, closing_time)

    open_value, close_value = split_opening_hours(opening_hours)
    return build_opening_hours(open_value, close_value)


# Tách chuỗi giờ mở cửa thành cặp giờ mở và giờ đóng.
def split_opening_hours(opening_hours: Optional[str]) -> tuple[str, str]:
    raw = (opening_hours or "").strip()
    if not raw:
        return "", ""
    normalized = raw.replace("–", "-").replace("—", "-")
    parts = [part.strip() for part in normalized.split("-") if part.strip()]
    if len(parts) >= 2:
        return parts[0], parts[1]
    return raw, ""


# Lấy 3 món đặc sản từ object bất kỳ có cùng tên thuộc tính.
def serialize_specialties(source) -> list[str]:
    return normalize_specialty_values(
        getattr(source, "specialty_1", ""),
        getattr(source, "specialty_2", ""),
        getattr(source, "specialty_3", "")
    )


# Chuẩn hóa chuỗi tìm kiếm để so khớp không dấu và không phân biệt hoa thường.
def normalize_search_text(value: Optional[str]) -> str:
    raw = (value or "").strip().lower()
    if not raw:
        return ""
    normalized = unicodedata.normalize("NFD", raw)
    without_marks = "".join(ch for ch in normalized if unicodedata.category(ch) != "Mn")
    return without_marks.replace("đ", "d")


# Tách câu truy vấn thành các từ khóa tìm kiếm độc lập.
def split_search_terms(value: Optional[str]) -> list[str]:
    return [item for item in normalize_search_text(value).split() if item]


# Định dạng khoảng cách sang mét hoặc km để hiển thị cho người dùng.
def format_distance_text(distance_km: float) -> str:
    if distance_km < 1:
        return f"{int(round(distance_km * 1000))}m"
    return f"{round(distance_km, 2)}km"


# Chuyển dữ liệu gian hàng sang payload chung dùng cho app và web.
def serialize_stall_card(stall: Stall, request: Request, distance_km: float) -> dict:
    opening_time, closing_time = split_opening_hours(stall.opening_hours)
    qr_code = build_stall_qr_code(stall.id)
    specialties = serialize_specialties(stall)
    return {
        "Id": stall.id,
        "Name": stall.name,
        "DistanceText": format_distance_text(distance_km),
        "Distance": distance_km,
        "Lat": stall.latitude,
        "Lng": stall.longitude,
        "Rating": str(stall.rating_avg),
        "Reviews": f"({stall.reviews_count})",
        "ReviewsCount": stall.reviews_count,
        "Cuisine": stall.category.name if stall.category else "",
        "CategorySlug": stall.category.slug if stall.category else "",
        "OpeningHours": stall.opening_hours or "",
        "OpeningTime": opening_time,
        "ClosingTime": closing_time,
        "Specialties": specialties,
        "SpecialtyTranslations": build_specialty_translations(specialties),
        "PoiRadiusMeters": stall.poi_radius_m or 30,
        "Translations": translations_to_dict(stall),
        "ImageUrl": build_upload_url(request, stall.image_url) if stall.image_url else "",
        "ThumbnailUrl": build_thumbnail_url(request, stall.image_url) if stall.image_url else "",
        "QrCodeValue": qr_code,
        "QrLaunchUrl": str(request.base_url).rstrip("/") + f"/qr/resolve?code={qr_code}"
    }


# Sinh phiên bản đồng bộ nội dung dựa trên thời điểm cập nhật mới nhất.
def get_content_sync_version(db: Session) -> str:
    stall_updated = db.query(func.max(Stall.updated_at)).scalar()
    translation_updated = db.query(func.max(StallTranslation.updated_at)).scalar()
    category_updated = db.query(func.max(Category.updated_at)).scalar()

    timestamps = [
        item for item in (stall_updated, translation_updated, category_updated)
        if item is not None
    ]

    if not timestamps:
        return "0"

    latest = max(timestamps)
    return latest.isoformat(timespec="microseconds")


# Tính lại tổng số review và điểm trung bình của gian hàng.
def refresh_stall_rating_summary(db: Session, stall: Stall) -> None:
    review_count, review_sum = (
        db.query(
            func.count(Review.id),
            func.coalesce(func.sum(Review.rating), 0)
        )
        .filter(
            Review.stall_id == stall.id,
            Review.is_approved == True,
            Review.is_deleted == False
        )
        .one()
    )

    actual_review_count = int(review_count or 0)
    actual_review_sum = float(review_sum or 0)

    current_review_count = int(stall.reviews_count or 0)
    current_rating_avg = float(stall.rating_avg or 0)

    baseline_review_count = max(current_review_count - actual_review_count, 0)
    baseline_review_sum = max((current_rating_avg * current_review_count) - actual_review_sum, 0)

    combined_review_count = baseline_review_count + actual_review_count
    combined_review_sum = baseline_review_sum + actual_review_sum
    combined_rating_avg = (combined_review_sum / combined_review_count) if combined_review_count > 0 else 0

    stall.reviews_count = combined_review_count
    stall.rating_avg = round(combined_rating_avg, 1)
    stall.updated_at = datetime.utcnow()


# Làm tròn số thực đến 1 chữ số thập phân theo quy tắc hiện tại.
def rounded_tenth(value: float) -> float:
    return math.floor((value * 10) + 0.5) / 10.0


# Tìm tổng điểm mục tiêu để khôi phục dữ liệu review tổng hợp cũ.
def choose_target_total_sum(current_count: int, current_avg: float, actual_sum: int, baseline_count: int) -> Optional[int]:
    if current_count <= 0:
        return 0

    desired_avg = rounded_tenth(current_avg)
    desired_sum = current_avg * current_count
    candidates: list[int] = []

    for total_sum in range(current_count, (current_count * 5) + 1):
        if rounded_tenth(total_sum / current_count) != desired_avg:
            continue

        baseline_sum = total_sum - actual_sum
        if baseline_count == 0 and baseline_sum != 0:
            continue
        if baseline_count > 0 and not (baseline_count <= baseline_sum <= baseline_count * 5):
            continue

        candidates.append(total_sum)

    if not candidates:
        return None

    return min(candidates, key=lambda total_sum: abs(total_sum - desired_sum))


# Tách một tổng điểm thành danh sách rating 1-5 dùng cho dữ liệu bù.
def split_sum_into_ratings(total_sum: int, count: int) -> list[int]:
    if count <= 0:
        return []

    ratings = [1] * count
    remaining = total_sum - count
    index = 0

    while remaining > 0:
        increment = min(4, remaining)
        ratings[index] += increment
        remaining -= increment
        index += 1
        if index >= count:
            index = 0

    return ratings


# Khôi phục các review giả lập cho dữ liệu cũ chỉ còn aggregate.
def repair_legacy_review_aggregates():
    db = SessionLocal()
    try:
        stalls = db.query(Stall).filter(Stall.is_deleted == False).all()
        touched_stalls = 0

        for stall in stalls:
            actual_review_count, actual_review_sum = (
                db.query(
                    func.count(Review.id),
                    func.coalesce(func.sum(Review.rating), 0)
                )
                .filter(
                    Review.stall_id == stall.id,
                    Review.is_approved == True,
                    Review.is_deleted == False
                )
                .one()
            )

            current_count = int(stall.reviews_count or 0)
            current_avg = float(stall.rating_avg or 0)
            actual_count = int(actual_review_count or 0)
            actual_sum = int(actual_review_sum or 0)
            baseline_count = current_count - actual_count

            if baseline_count > 0 and current_count > 0 and current_avg > 0:
                target_total_sum = choose_target_total_sum(current_count, current_avg, actual_sum, baseline_count)
                if target_total_sum is not None:
                    baseline_sum = target_total_sum - actual_sum
                    if baseline_count <= baseline_sum <= baseline_count * 5:
                        for rating in split_sum_into_ratings(baseline_sum, baseline_count):
                            db.add(Review(
                                stall_id=stall.id,
                                rating=rating,
                                ip_address=None,
                                comment=LEGACY_REVIEW_COMMENT,
                                reviewer_name=LEGACY_REVIEWER_NAME,
                                is_approved=True,
                                created_at=datetime.utcnow(),
                                updated_at=datetime.utcnow(),
                                is_deleted=False
                            ))
                        db.flush()
                        touched_stalls += 1

            refresh_stall_rating_summary(db, stall)

        if touched_stalls > 0:
            print(f"legacy review aggregates repaired for {touched_stalls} stalls")
        db.commit()
    except Exception:
        db.rollback()
        raise
    finally:
        db.close()


repair_legacy_review_aggregates()


# Lấy địa chỉ IP thực ưu tiên từ header proxy nếu có.
def get_request_ip(request: Request) -> str:
    forwarded = request.headers.get("x-forwarded-for", "")
    if forwarded:
        first_hop = forwarded.split(",")[0].strip()
        if first_hop:
            return first_hop
    return request.client.host if request.client else ""


# Tạo điều kiện nhận diện người dùng để chống ghi log trùng lặp.
def build_activity_identity_filters(model, session_id: str = "", device_id: str = "", ip_address: str = ""):
    normalized_session = (session_id or "").strip()
    normalized_device = (device_id or "").strip()
    normalized_ip = (ip_address or "").strip()
    if normalized_device:
        return [model.device_id == normalized_device]
    if normalized_session:
        return [model.session_id == normalized_session]
    if normalized_ip:
        return [model.ip_address == normalized_ip]
    return []


# Kiểm tra người dùng đã phát sinh hoạt động tương tự trong cửa sổ thời gian gần chưa.
def has_recent_activity(
    db: Session,
    model,
    occurred_column,
    stall_id: int,
    window_seconds: int,
    session_id: str = "",
    device_id: str = "",
    ip_address: str = ""
) -> bool:
    identity_filters = build_activity_identity_filters(
        model,
        session_id=session_id,
        device_id=device_id,
        ip_address=ip_address
    )
    if not identity_filters:
        return False

    cutoff = datetime.utcnow() - timedelta(seconds=window_seconds)
    recent_row = (
        db.query(model.id)
        .filter(
            model.stall_id == stall_id,
            occurred_column >= cutoff,
            or_(*identity_filters)
        )
        .order_by(occurred_column.desc())
        .first()
    )
    return recent_row is not None


# Sinh khóa nhận diện người dùng từ location log để gom nhóm trên dashboard.
def get_location_log_user_key(row: "LocationLog") -> str:
    return row.device_id or row.session_id or f"anon:{row.id}"


# Tính điểm phù hợp của gian hàng với bộ từ khóa tìm kiếm.
def compute_search_score(stall: Stall, query_terms: list[str]) -> int:
    if not query_terms:
        return 0

    name = normalize_search_text(stall.name)
    category_name = normalize_search_text(stall.category.name if stall.category else "")
    category_slug = normalize_search_text(stall.category.slug if stall.category else "")
    specialties = [normalize_search_text(item) for item in serialize_specialties(stall)]

    score = 0
    for term in query_terms:
        matched = False
        if term in name:
            score += 120 if name.startswith(term) else 90
            matched = True
        elif any(term in item for item in specialties):
            score += 80
            matched = True
        elif term in category_name or term in category_slug:
            score += 50
            matched = True

        if not matched:
            return 0

    return score


# Ánh xạ mã ngôn ngữ nội bộ sang mã mà gTTS hỗ trợ.
def map_tts_language(language_code: str) -> str:
    return {
        "vi": "vi",
        "en": "en",
        "ja": "ja",
        "ko": "ko",
        "zh-CN": "zh-CN",
    }.get(language_code, "vi")


# Sinh dữ liệu MP3 từ script giới thiệu bằng gTTS.
def generate_audio_bytes(script_text: str, language_code: str) -> bytes:
    audio_buffer = BytesIO()
    tts = gTTS(text=script_text, lang=map_tts_language(language_code))
    tts.write_to_fp(audio_buffer)
    return audio_buffer.getvalue()


# Tạo hash nội dung script để kiểm tra audio cache còn hợp lệ hay không.
def get_script_hash(script_text: str, language_code: str) -> str:
    content = f"{AUDIO_PROFILE_VERSION}:{language_code}:{script_text}".encode("utf-8")
    return hashlib.sha256(content).hexdigest()


# Chuyển model người dùng sang payload JSON gọn cho client.
def serialize_user(user: User) -> dict:
    return {
        "id": user.id,
        "full_name": user.full_name,
        "username": user.username,
        "email": user.email,
        "is_active": user.is_active,
        "role": user.role.name if user.role else None
    }


# Chuyển dữ liệu owner sang payload quản trị kèm gian hàng nếu có.
def serialize_admin_user(request: Request, user: User, stall: Optional[Stall] = None) -> dict:
    payload = serialize_user(user)
    payload["stall_name"] = stall.name if stall else ""
    payload["stall"] = serialize_owner_stall_for_request(request, stall) if stall else None
    return payload


# Chuyển gian hàng sang payload quản lý dành cho chủ quán.
def serialize_owner_stall(stall: Stall) -> dict:
    opening_time, closing_time = split_opening_hours(stall.opening_hours)
    return {
        "id": stall.id,
        "name": stall.name,
        "category_id": stall.category_id,
        "lat": stall.latitude,
        "lng": stall.longitude,
        "specialties": serialize_specialties(stall),
        "poi_radius_m": stall.poi_radius_m or 30,
        "opening_hours": stall.opening_hours or "",
        "opening_time": opening_time,
        "closing_time": closing_time,
        "is_open": stall.is_open,
        "rating_avg": float(stall.rating_avg or 0),
        "reviews_count": int(stall.reviews_count or 0),
        "script_vi": translations_to_dict(stall).get("vi", ""),
        "image_url": f"/uploads/{stall.image_url}" if stall.image_url else ""
    }


# Lấy script tiếng Việt của gian hàng nếu tồn tại.
def get_stall_script_vi(stall: Optional[Stall]) -> str:
    if not stall:
        return ""
    return translations_to_dict(stall).get("vi", "")


# Chuyển gian hàng hiện tại thành payload để so sánh với yêu cầu cập nhật.
def serialize_stall_for_compare(request: Request, stall: Optional[Stall]) -> dict:
    if not stall:
        return {}
    opening_time, closing_time = split_opening_hours(stall.opening_hours)

    image_url = ""
    if stall.image_url:
        image_url = str(request.base_url).rstrip("/") + f"/uploads/{stall.image_url}"

    return {
        "name": stall.name or "",
        "category_id": stall.category_id,
        "category_name": stall.category.name if getattr(stall, "category", None) else "",
        "lat": stall.latitude,
        "lng": stall.longitude,
        "opening_hours": stall.opening_hours or "",
        "opening_time": opening_time,
        "closing_time": closing_time,
        "is_open": bool(stall.is_open),
        "poi_radius_m": stall.poi_radius_m or 30,
        "specialties": serialize_specialties(stall),
        "script_vi": get_stall_script_vi(stall),
        "image_url": image_url,
    }


# Chuyển phần dữ liệu mới trong yêu cầu cập nhật sang payload so sánh.
def serialize_update_request_new_values(request: Request, row: StallUpdateRequest) -> dict:
    opening_time, closing_time = split_opening_hours(row.opening_hours)
    image_url = ""
    if row.image_url:
        image_url = str(request.base_url).rstrip("/") + f"/uploads/{row.image_url}"

    return {
        "name": row.name or "",
        "category_id": row.category_id,
        "category_name": row.category.name if getattr(row, "category", None) else "",
        "lat": row.latitude,
        "lng": row.longitude,
        "opening_hours": row.opening_hours or "",
        "opening_time": opening_time,
        "closing_time": closing_time,
        "is_open": bool(row.is_open),
        "poi_radius_m": row.poi_radius_m or 30,
        "specialties": serialize_specialties(row),
        "script_vi": row.script_vi or "",
        "image_url": image_url,
    }


# So sánh từng trường giữa dữ liệu hiện tại và dữ liệu chủ quán yêu cầu sửa.
def build_request_field_changes(current_values: dict, requested_values: dict) -> list[dict]:
    field_specs = [
        ("name", "Tên gian hàng"),
        ("category_name", "Danh mục"),
        ("lat", "Vĩ độ"),
        ("lng", "Kinh độ"),
        ("opening_time", "Giờ mở"),
        ("closing_time", "Giờ đóng"),
        ("is_open", "Trạng thái mở cửa"),
        ("poi_radius_m", "Bán kính tự phát POI"),
        ("specialties", "3 món đặc sản"),
        ("script_vi", "Script tiếng Việt"),
        ("image_url", "Ảnh gian hàng"),
    ]

    changes = []
    for key, label in field_specs:
        old_value = current_values.get(key)
        new_value = requested_values.get(key)
        changed = old_value != new_value
        changes.append({
            "key": key,
            "label": label,
            "old_value": old_value,
            "new_value": new_value,
            "changed": changed,
        })
    return changes


# Tạo payload chi tiết cho một yêu cầu cập nhật gian hàng.
def serialize_update_request_detail(request: Request, row: StallUpdateRequest) -> dict:
    current_values = serialize_stall_for_compare(request, row.stall)
    requested_values = serialize_update_request_new_values(request, row)
    return {
        "id": row.id,
        "stall_id": row.stall_id,
        "stall_name": row.stall.name if row.stall else "",
        "status": row.status,
        "admin_note": row.admin_note or "",
        "submitted_at": row.submitted_at.isoformat() if row.submitted_at else None,
        "reviewed_at": row.reviewed_at.isoformat() if row.reviewed_at else None,
        "owner_read_at": row.owner_read_at.isoformat() if row.owner_read_at else None,
        "is_read": row.owner_read_at is not None,
        "current_values": current_values,
        "requested_values": requested_values,
        "field_changes": build_request_field_changes(current_values, requested_values),
    }


# Chuyển người nhận thông báo admin sang dữ liệu hiển thị trên dashboard.
def serialize_admin_notification_recipient(request: Request, user: User, stall: Optional[Stall] = None) -> dict:
    display_name = (user.full_name or "").strip() or (stall.name if stall else "") or user.username
    return {
        "id": user.id,
        "full_name": display_name,
        "username": user.username,
        "email": user.email,
        "is_active": bool(user.is_active),
        "stall_name": stall.name if stall else "",
    }


# Chuyển lịch sử thông báo admin và trạng thái đọc của người nhận.
def serialize_admin_notification_history(row: AdminNotification, recipients: list[AdminNotificationRecipient]) -> dict:
    active_recipients = [item for item in recipients if not item.deleted]
    read_count = sum(1 for item in active_recipients if item.read_at is not None)
    return {
        "id": row.id,
        "title": row.title,
        "message": row.message,
        "recipient_scope": row.recipient_scope,
        "created_at": row.created_at.isoformat() if row.created_at else None,
        "updated_at": row.updated_at.isoformat() if row.updated_at else None,
        "created_by": row.creator.full_name if row.creator and row.creator.full_name else (row.creator.username if row.creator else ""),
        "recipient_count": len(active_recipients),
        "read_count": read_count,
        "recipients": [
            {
                "user_id": item.user_id,
                "full_name": (item.user.full_name or "").strip() if item.user else "",
                "username": item.user.username if item.user else "",
                "read_at": item.read_at.isoformat() if item.read_at else None,
            }
            for item in active_recipients
        ],
    }


# Chuyển một thông báo admin sang payload hiển thị cho chủ quán.
def serialize_owner_admin_notification(item: AdminNotificationRecipient) -> dict:
    notification = item.notification
    return {
        "source": "admin_notification",
        "id": item.id,
        "notification_id": notification.id if notification else None,
        "title": notification.title if notification else "",
        "message": notification.message if notification else "",
        "recipient_scope": notification.recipient_scope if notification else "",
        "created_at": notification.created_at.isoformat() if notification and notification.created_at else None,
        "updated_at": notification.updated_at.isoformat() if notification and notification.updated_at else None,
        "is_read": item.read_at is not None,
        "read_at": item.read_at.isoformat() if item.read_at else None,
    }


# Biến yêu cầu cập nhật thành một dòng thông báo trong hộp thư chủ quán.
def serialize_owner_request_notification(request: Request, row: StallUpdateRequest) -> dict:
    payload = serialize_update_request_detail(request, row)
    payload["source"] = "update_request"
    payload["title"] = (
        "Yêu cầu đã được duyệt"
        if row.status == "approved"
        else "Yêu cầu bị từ chối"
        if row.status == "rejected"
        else "Yêu cầu đang chờ duyệt"
    )
    payload["message"] = row.admin_note or ""
    payload["created_at"] = payload["submitted_at"]
    return payload


# Lấy danh sách owner nhận thông báo theo phạm vi gửi mà admin chọn.
def get_notification_recipient_users(db: Session, scope: str, user_ids: Optional[list[int]] = None) -> list[User]:
    query = (
        db.query(User)
        .join(Role, User.role_id == Role.id)
        .options(joinedload(User.role))
        .filter(Role.name == "stall_owner")
    )
    if scope == "active_owners":
        query = query.filter(User.is_active == True)
    elif scope == "selected_users":
        normalized_ids = sorted({int(item) for item in (user_ids or []) if item})
        if not normalized_ids:
            raise HTTPException(status_code=400, detail="Vui lòng chọn ít nhất một người nhận")
        query = query.filter(User.id.in_(normalized_ids))
    elif scope != "all_owners":
        raise HTTPException(status_code=400, detail="Nhóm người nhận không hợp lệ")
    return query.order_by(User.full_name.asc(), User.username.asc()).all()


# Chuẩn hóa dữ liệu tham chiếu như role, ngôn ngữ, danh mục và admin mặc định.
def normalize_reference_data():
    db = SessionLocal()
    try:
        role_descriptions = {
            "super_admin": "Quản trị hệ thống",
            "stall_owner": "Chủ gian hàng",
        }
        for role_name, description in role_descriptions.items():
            role = db.query(Role).filter(Role.name == role_name).first()
            if role and role.description != description:
                role.description = description
                role.updated_at = datetime.utcnow()

        languages = {
            "vi": ("Vietnamese", "Tiếng Việt", "vi-VN", 1),
            "en": ("English", "English", "en-US", 2),
            "zh-CN": ("Chinese", "中文", "zh-CN", 3),
            "ja": ("Japanese", "日本語", "ja-JP", 4),
            "ko": ("Korean", "한국어", "ko-KR", 5),
        }
        for code, (name, native_name, locale_code, sort_order) in languages.items():
            item = db.query(Language).filter(Language.code == code).first()
            if item:
                item.name = name
                item.native_name = native_name
                item.locale_code = locale_code
                item.sort_order = sort_order
                item.updated_at = datetime.utcnow()

        categories = {
            "cat-1": "Hải sản",
            "cat-2": "Đồ nướng",
            "cat-3": "Món nước",
            "cat-4": "Ăn vặt",
            "cat-5": "Tráng miệng",
        }
        for slug, name in categories.items():
            item = db.query(Category).filter(Category.slug == slug).first()
            if item and item.name != name:
                item.name = name
                item.updated_at = datetime.utcnow()

        if SEED_DEFAULT_ADMIN:
            admin_role = db.query(Role).filter(Role.name == "super_admin").first()
            admin_user = db.query(User).filter(User.username == "admin").first()
            if admin_role and not admin_user:
                db.add(User(
                    role_id=admin_role.id,
                    username="admin",
                    password_hash=hash_password(DEFAULT_ADMIN_PASSWORD),
                    full_name="Quản trị hệ thống",
                    email="admin@streetfeast.local",
                    is_active=True
                ))
            elif admin_user and needs_password_rehash(admin_user.password_hash):
                admin_user.password_hash = hash_password(DEFAULT_ADMIN_PASSWORD)
                admin_user.updated_at = datetime.utcnow()

        db.commit()
    finally:
        db.close()


# Khởi tạo dữ liệu gốc tối thiểu khi database còn thiếu.
def ensure_seed_data():
    db = SessionLocal()
    try:
        if not db.query(Role).filter(Role.name == "super_admin").first():
            db.add(Role(name="super_admin", description="Quản trị hệ thống"))
        if not db.query(Role).filter(Role.name == "stall_owner").first():
            db.add(Role(name="stall_owner", description="Chủ gian hàng"))
        db.commit()

        default_languages = [
            ("vi", "Vietnamese", "Tiếng Việt", "vi-VN", 1),
            ("en", "English", "English", "en-US", 2),
            ("zh-CN", "Chinese", "中文", "zh-CN", 3),
            ("ja", "Japanese", "日本語", "ja-JP", 4),
            ("ko", "Korean", "한국어", "ko-KR", 5),
        ]
        for code, name, native_name, locale_code, sort_order in default_languages:
            if not db.query(Language).filter(Language.code == code).first():
                db.add(Language(code=code, name=name, native_name=native_name, locale_code=locale_code, sort_order=sort_order))

        default_categories = ["Hải sản", "Đồ nướng", "Món nước", "Ăn vặt", "Tráng miệng"]
        for idx, name in enumerate(default_categories, start=1):
            slug = f"cat-{idx}"
            if not db.query(Category).filter(Category.slug == slug).first():
                db.add(Category(slug=slug, name=name))

        db.commit()

        admin_role = db.query(Role).filter(Role.name == "super_admin").first()
        if SEED_DEFAULT_ADMIN and admin_role and not db.query(User).filter(User.username == "admin").first():
            db.add(User(
                role_id=admin_role.id,
                username="admin",
                password_hash=hash_password(DEFAULT_ADMIN_PASSWORD),
                full_name="Quản trị hệ thống",
                email="admin@streetfeast.local",
                is_active=True
            ))
            db.commit()
    finally:
        db.close()


# Đảm bảo tài khoản web mặc định tồn tại và đúng thông tin cấu hình.
def ensure_default_web_users():
    db = SessionLocal()
    try:
        super_admin_role = db.query(Role).filter(Role.name == "super_admin").first()

        default_users = []
        if super_admin_role:
            default_users.append({
                "role_id": super_admin_role.id,
                "username": "admin",
                "password": DEFAULT_ADMIN_PASSWORD,
                "full_name": "Quản trị hệ thống",
                "email": "admin@streetfeast.local",
            })

        for item in default_users:
            user = db.query(User).filter(User.username == item["username"]).first()
            if not user:
                db.add(User(
                    role_id=item["role_id"],
                    username=item["username"],
                    password_hash=hash_password(item["password"]),
                    full_name=item["full_name"],
                    email=item["email"],
                    is_active=True
                ))
                continue

            user.role_id = item["role_id"]
            user.full_name = item["full_name"]
            user.email = item["email"]
            user.is_active = True
            user.updated_at = datetime.utcnow()
            if needs_password_rehash(user.password_hash):
                user.password_hash = hash_password(item["password"])

        db.commit()
    finally:
        db.close()


# Giữ lại hàm cũ để tương thích với script legacy, không còn dùng nghiệp vụ mới.
def ensure_stall_owner_assignments():
    return


# Bổ sung các bản dịch còn thiếu cho những gian hàng đã có script tiếng Việt.
def ensure_stall_translation_coverage():
    db = SessionLocal()
    try:
        expected_codes = set(SUPPORTED_TRANSLATIONS.keys())
        stalls = (
            db.query(Stall)
            .options(joinedload(Stall.translations).joinedload(StallTranslation.language))
            .filter(Stall.is_deleted == False)
            .all()
        )

        changed = False
        for stall in stalls:
            current_translations = translations_to_dict(stall)
            base_script_vi = current_translations.get("vi", "").strip()
            if not base_script_vi:
                continue

            current_codes = {
                item.language.code
                for item in stall.translations
                if item.language and item.script_text and item.script_text.strip()
            }
            missing_codes = expected_codes - current_codes
            if not missing_codes:
                continue

            upsert_stall_translations(db, stall, stall.name or "", base_script_vi)
            changed = True

        if changed:
            db.commit()
    finally:
        db.close()


# Tự tạo lại script giới thiệu đủ độ dài nếu dữ liệu cũ còn quá ngắn.
def ensure_minimum_stall_script_length():
    db = SessionLocal()
    try:
        changed = False

        stalls = (
            db.query(Stall)
            .options(
                joinedload(Stall.category),
                joinedload(Stall.translations).joinedload(StallTranslation.language)
            )
            .filter(Stall.is_deleted == False)
            .all()
        )
        for stall in stalls:
            current_script_vi = translations_to_dict(stall).get("vi", "").strip()
            if count_words(current_script_vi) >= MIN_STALL_SCRIPT_WORDS:
                continue

            rebuilt_script_vi = build_verbose_stall_intro(
                stall.name or "",
                stall.category.name if stall.category else "",
                serialize_specialties(stall),
                stall.opening_hours or "",
                stall.poi_radius_m or 30
            )
            upsert_stall_translations(db, stall, stall.name or "", rebuilt_script_vi)
            changed = True

        requests = (
            db.query(StallUpdateRequest)
            .options(joinedload(StallUpdateRequest.category))
            .all()
        )
        for row in requests:
            if count_words((row.script_vi or "").strip()) >= MIN_STALL_SCRIPT_WORDS:
                continue

            row.script_vi = build_verbose_stall_intro(
                row.name or "",
                row.category.name if row.category else "",
                serialize_specialties(row),
                row.opening_hours or "",
                row.poi_radius_m or 30
            )
            changed = True

        if changed:
            db.commit()
    finally:
        db.close()


# Làm sạch lại tên hiển thị của dữ liệu tham chiếu theo chuẩn hiện tại.
def repair_reference_data_clean():
    db = SessionLocal()
    try:
        role_descriptions = {
            "super_admin": "Quản trị hệ thống",
            "stall_owner": "Chủ gian hàng",
        }
        for role_name, description in role_descriptions.items():
            role = db.query(Role).filter(Role.name == role_name).first()
            if role:
                role.description = description
                role.updated_at = datetime.utcnow()

        languages = {
            "vi": ("Vietnamese", "Tiếng Việt", "vi-VN", 1),
            "en": ("English", "English", "en-US", 2),
            "zh-CN": ("Chinese", "中文", "zh-CN", 3),
            "ja": ("Japanese", "日本語", "ja-JP", 4),
            "ko": ("Korean", "한국어", "ko-KR", 5),
        }
        for code, (name, native_name, locale_code, sort_order) in languages.items():
            item = db.query(Language).filter(Language.code == code).first()
            if item:
                item.name = name
                item.native_name = native_name
                item.locale_code = locale_code
                item.sort_order = sort_order
                item.updated_at = datetime.utcnow()

        categories = {
            "cat-1": "Hải sản",
            "cat-2": "Đồ nướng",
            "cat-3": "Món nước",
            "cat-4": "Ăn vặt",
            "cat-5": "Tráng miệng",
        }
        for slug, name in categories.items():
            item = db.query(Category).filter(Category.slug == slug).first()
            if item:
                item.name = name
                item.updated_at = datetime.utcnow()

        admin_user = db.query(User).filter(User.username == "admin").first()
        if admin_user:
            admin_user.full_name = "Quản trị hệ thống"
            admin_user.updated_at = datetime.utcnow()

        db.commit()
    finally:
        db.close()


ensure_seed_data()
ensure_default_web_users()
ensure_stall_translation_coverage()
ensure_minimum_stall_script_length()
normalize_reference_data()
repair_reference_data_clean()
