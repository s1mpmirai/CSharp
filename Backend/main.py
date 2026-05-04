from fastapi import FastAPI, Depends, File, UploadFile, Form, HTTPException, Request
from fastapi.staticfiles import StaticFiles
from fastapi.responses import FileResponse, JSONResponse, RedirectResponse, Response
from pydantic import BaseModel
from sqlalchemy import Column, Integer, String, Float, Text, ForeignKey, Boolean, DateTime, BigInteger, Numeric, LargeBinary, create_engine, func, text, or_
from sqlalchemy.ext.declarative import declarative_base
from sqlalchemy.orm import sessionmaker, Session, relationship, joinedload
from fastapi.middleware.cors import CORSMiddleware
from geopy.distance import geodesic
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

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
DATABASE_URL = os.getenv("DATABASE_URL", "postgresql://admin:password123@localhost:5432/food_street_db")
APP_SECRET = os.getenv("APP_SECRET", "streetfeast-secret-key")
SESSION_COOKIE = "sf_session"
WEB_DIR = os.path.abspath(os.getenv("WEB_DIR", os.path.join(BASE_DIR, "..", "Web")))
UPLOAD_DIR = os.path.join(BASE_DIR, "uploads")
THUMBNAIL_MAX_SIZE = int(os.getenv("THUMBNAIL_MAX_SIZE", "160"))
TOKEN_HOURS = 12
PBKDF2_ITERATIONS = int(os.getenv("PBKDF2_ITERATIONS", "390000"))
COOKIE_SECURE = os.getenv("COOKIE_SECURE", "false").lower() == "true"
DEFAULT_ADMIN_PASSWORD = os.getenv("DEFAULT_ADMIN_PASSWORD", "admin123")
SEED_DEFAULT_ADMIN = os.getenv("SEED_DEFAULT_ADMIN", "true").lower() == "true"
CORS_ORIGINS = [item.strip() for item in os.getenv("CORS_ORIGINS", "*").split(",") if item.strip()]

engine = create_engine(DATABASE_URL)
SessionLocal = sessionmaker(autocommit=False, autoflush=False, bind=engine)
Base = declarative_base()

SUPPORTED_TRANSLATIONS = {
    "vi": "vi",
    "en": "en",
    "ko": "ko",
    "ja": "ja",
    "zh-CN": "zh-CN",
}
AUDIO_PROFILE_VERSION = "gtts-v2"
MIN_STALL_SCRIPT_WORDS = 100
LEGACY_REVIEWER_NAME = "__legacy_rating_backfill__"
LEGACY_REVIEW_COMMENT = "Synthetic review row created to preserve legacy aggregate rating data."
LISTEN_DEDUP_WINDOW_SECONDS = 10
QR_DEDUP_WINDOW_SECONDS = 10


class Role(Base):
    __tablename__ = "roles"
    id = Column(Integer, primary_key=True, index=True)
    name = Column(String(50), nullable=False, unique=True)
    description = Column(Text)
    created_at = Column(DateTime, nullable=False, default=datetime.utcnow)
    updated_at = Column(DateTime, nullable=False, default=datetime.utcnow)


class User(Base):
    __tablename__ = "users"
    id = Column(Integer, primary_key=True, index=True)
    role_id = Column(Integer, ForeignKey("roles.id"), nullable=False)
    username = Column(String(100), nullable=False, unique=True)
    password_hash = Column(Text, nullable=False)
    full_name = Column(String(150))
    email = Column(String(150), unique=True)
    is_active = Column(Boolean, nullable=False, default=True)
    created_at = Column(DateTime, nullable=False, default=datetime.utcnow)
    updated_at = Column(DateTime, nullable=False, default=datetime.utcnow)
    role = relationship("Role")


class Language(Base):
    __tablename__ = "languages"
    id = Column(Integer, primary_key=True, index=True)
    code = Column(String(16), nullable=False, unique=True)
    name = Column(String(100), nullable=False)
    native_name = Column(String(100), nullable=False)
    locale_code = Column(String(20), nullable=False)
    is_active = Column(Boolean, nullable=False, default=True)
    sort_order = Column(Integer, nullable=False, default=0)
    created_at = Column(DateTime, nullable=False, default=datetime.utcnow)
    updated_at = Column(DateTime, nullable=False, default=datetime.utcnow)


class Category(Base):
    __tablename__ = "categories"
    id = Column(Integer, primary_key=True, index=True)
    slug = Column(String(100), nullable=False, unique=True)
    name = Column(String(120), nullable=False)
    icon_url = Column(Text)
    is_active = Column(Boolean, nullable=False, default=True)
    created_at = Column(DateTime, nullable=False, default=datetime.utcnow)
    updated_at = Column(DateTime, nullable=False, default=datetime.utcnow)


class Stall(Base):
    __tablename__ = "stalls"
    id = Column(Integer, primary_key=True, index=True)
    category_id = Column(Integer, ForeignKey("categories.id"))
    name = Column(String(200), nullable=False)
    latitude = Column(Float, nullable=False)
    longitude = Column(Float, nullable=False)
    image_url = Column(Text)
    specialty_1 = Column(Text)
    specialty_2 = Column(Text)
    specialty_3 = Column(Text)
    poi_radius_m = Column(Float, nullable=False, default=30)
    opening_hours = Column(String(255))
    is_open = Column(Boolean, nullable=False, default=True)
    is_active = Column(Boolean, nullable=False, default=True)
    rating_avg = Column(Numeric(2, 1), nullable=False, default=0)
    reviews_count = Column(Integer, nullable=False, default=0)
    created_by_user_id = Column(Integer, ForeignKey("users.id"))
    created_at = Column(DateTime, nullable=False, default=datetime.utcnow)
    updated_at = Column(DateTime, nullable=False, default=datetime.utcnow)
    is_deleted = Column(Boolean, nullable=False, default=False)

    category = relationship("Category")
    translations = relationship("StallTranslation", back_populates="stall", cascade="all, delete-orphan")


class StallTranslation(Base):
    __tablename__ = "stall_translations"
    id = Column(Integer, primary_key=True, index=True)
    stall_id = Column(Integer, ForeignKey("stalls.id", ondelete="CASCADE"), nullable=False, index=True)
    language_id = Column(Integer, ForeignKey("languages.id"), nullable=False, index=True)
    title = Column(String(200))
    description = Column(Text)
    script_text = Column(Text, nullable=False)
    is_auto_generated = Column(Boolean, nullable=False, default=True)
    translation_status = Column(String(30), nullable=False, default="draft")
    source_version = Column(Integer, nullable=False, default=1)
    created_at = Column(DateTime, nullable=False, default=datetime.utcnow)
    updated_at = Column(DateTime, nullable=False, default=datetime.utcnow)

    stall = relationship("Stall", back_populates="translations")
    language = relationship("Language")


class Review(Base):
    __tablename__ = "reviews"
    id = Column(Integer, primary_key=True, index=True)
    stall_id = Column(Integer, ForeignKey("stalls.id", ondelete="CASCADE"), nullable=False)
    rating = Column(Integer, nullable=False)
    ip_address = Column(String(64))
    comment = Column(Text)
    reviewer_name = Column(String(120))
    is_approved = Column(Boolean, nullable=False, default=False)
    created_at = Column(DateTime, nullable=False, default=datetime.utcnow)
    updated_at = Column(DateTime, nullable=False, default=datetime.utcnow)
    is_deleted = Column(Boolean, nullable=False, default=False)


class ListeningLog(Base):
    __tablename__ = "listening_logs"
    id = Column(BigInteger, primary_key=True, index=True, autoincrement=True)
    stall_id = Column(Integer, ForeignKey("stalls.id", ondelete="CASCADE"), nullable=False)
    language_id = Column(Integer, ForeignKey("languages.id"), nullable=False)
    session_id = Column(String(120))
    device_id = Column(String(120))
    ip_address = Column(String(64))
    duration_seconds = Column(Integer, nullable=False, default=0)
    source = Column(String(30), nullable=False, default="app")
    latitude = Column(Float)
    longitude = Column(Float)
    listened_at = Column(DateTime, nullable=False, default=datetime.utcnow)


class StallAudioAsset(Base):
    __tablename__ = "stall_audio_assets"
    id = Column(Integer, primary_key=True, index=True)
    stall_id = Column(Integer, ForeignKey("stalls.id", ondelete="CASCADE"), nullable=False, index=True)
    language_id = Column(Integer, ForeignKey("languages.id"), nullable=False, index=True)
    script_hash = Column(String(64), nullable=False)
    mime_type = Column(String(120), nullable=False, default="audio/mpeg")
    audio_data = Column(LargeBinary, nullable=False)
    created_at = Column(DateTime, nullable=False, default=datetime.utcnow)
    updated_at = Column(DateTime, nullable=False, default=datetime.utcnow)


class StallUpdateRequest(Base):
    __tablename__ = "stall_update_requests"
    id = Column(Integer, primary_key=True, index=True)
    stall_id = Column(Integer, ForeignKey("stalls.id", ondelete="CASCADE"), nullable=False)
    submitted_by_user_id = Column(Integer, ForeignKey("users.id"), nullable=False)
    category_id = Column(Integer, ForeignKey("categories.id"))
    name = Column(String(200), nullable=False)
    latitude = Column(Float, nullable=False)
    longitude = Column(Float, nullable=False)
    specialty_1 = Column(Text)
    specialty_2 = Column(Text)
    specialty_3 = Column(Text)
    poi_radius_m = Column(Float, nullable=False, default=30)
    opening_hours = Column(String(255))
    is_open = Column(Boolean, nullable=False, default=True)
    script_vi = Column(Text, nullable=False)
    image_url = Column(Text)
    status = Column(String(20), nullable=False, default="pending")
    admin_note = Column(Text)
    submitted_at = Column(DateTime, nullable=False, default=datetime.utcnow)
    reviewed_at = Column(DateTime)
    reviewed_by_user_id = Column(Integer, ForeignKey("users.id"))
    owner_read_at = Column(DateTime)
    owner_deleted = Column(Boolean, nullable=False, default=False)

    stall = relationship("Stall")
    category = relationship("Category")


class AdminNotification(Base):
    __tablename__ = "admin_notifications"
    id = Column(Integer, primary_key=True, index=True)
    title = Column(String(200), nullable=False)
    message = Column(Text, nullable=False)
    recipient_scope = Column(String(30), nullable=False, default="selected_users")
    created_by_user_id = Column(Integer, ForeignKey("users.id"), nullable=False)
    created_at = Column(DateTime, nullable=False, default=datetime.utcnow)
    updated_at = Column(DateTime, nullable=False, default=datetime.utcnow)

    creator = relationship("User")


class AdminNotificationRecipient(Base):
    __tablename__ = "admin_notification_recipients"
    id = Column(Integer, primary_key=True, index=True)
    notification_id = Column(Integer, ForeignKey("admin_notifications.id", ondelete="CASCADE"), nullable=False, index=True)
    user_id = Column(Integer, ForeignKey("users.id"), nullable=False, index=True)
    read_at = Column(DateTime)
    deleted = Column(Boolean, nullable=False, default=False)
    created_at = Column(DateTime, nullable=False, default=datetime.utcnow)
    updated_at = Column(DateTime, nullable=False, default=datetime.utcnow)

    notification = relationship("AdminNotification")
    user = relationship("User")


class QrScanLog(Base):
    __tablename__ = "qr_scan_logs"
    id = Column(BigInteger, primary_key=True, index=True, autoincrement=True)
    stall_id = Column(Integer, ForeignKey("stalls.id", ondelete="CASCADE"), nullable=False, index=True)
    session_id = Column(String(120))
    device_id = Column(String(120))
    ip_address = Column(String(64))
    source = Column(String(30), nullable=False, default="qr")
    latitude = Column(Float)
    longitude = Column(Float)
    scanned_at = Column(DateTime, nullable=False, default=datetime.utcnow)


class LocationLog(Base):
    __tablename__ = "location_logs"
    id = Column(BigInteger, primary_key=True, index=True, autoincrement=True)
    session_id = Column(String(120))
    device_id = Column(String(120))
    latitude = Column(Float, nullable=False)
    longitude = Column(Float, nullable=False)
    source = Column(String(30), nullable=False, default="app")
    recorded_at = Column(DateTime, nullable=False, default=datetime.utcnow)


Base.metadata.create_all(bind=engine)


def ensure_schema_columns():
    statements = [
        "ALTER TABLE stalls ADD COLUMN IF NOT EXISTS specialty_1 TEXT",
        "ALTER TABLE stalls ADD COLUMN IF NOT EXISTS specialty_2 TEXT",
        "ALTER TABLE stalls ADD COLUMN IF NOT EXISTS specialty_3 TEXT",
        "ALTER TABLE stalls ADD COLUMN IF NOT EXISTS poi_radius_m DOUBLE PRECISION DEFAULT 30",
        "ALTER TABLE stall_update_requests ADD COLUMN IF NOT EXISTS specialty_1 TEXT",
        "ALTER TABLE stall_update_requests ADD COLUMN IF NOT EXISTS specialty_2 TEXT",
        "ALTER TABLE stall_update_requests ADD COLUMN IF NOT EXISTS specialty_3 TEXT",
        "ALTER TABLE stall_update_requests ADD COLUMN IF NOT EXISTS poi_radius_m DOUBLE PRECISION DEFAULT 30",
        "ALTER TABLE stall_update_requests ADD COLUMN IF NOT EXISTS owner_read_at TIMESTAMP",
        "ALTER TABLE stall_update_requests ADD COLUMN IF NOT EXISTS owner_deleted BOOLEAN NOT NULL DEFAULT FALSE",
        "ALTER TABLE listening_logs ADD COLUMN IF NOT EXISTS latitude DOUBLE PRECISION",
        "ALTER TABLE listening_logs ADD COLUMN IF NOT EXISTS longitude DOUBLE PRECISION",
        "ALTER TABLE listening_logs ADD COLUMN IF NOT EXISTS ip_address VARCHAR(64)",
        "ALTER TABLE reviews ADD COLUMN IF NOT EXISTS ip_address VARCHAR(64)",
    ]
    with engine.begin() as connection:
        for statement in statements:
            connection.execute(text(statement))


ensure_schema_columns()

app = FastAPI()
app.add_middleware(
    CORSMiddleware,
    allow_origins=CORS_ORIGINS or ["*"],
    allow_methods=["*"],
    allow_headers=["*"],
    allow_credentials=True,
)

os.makedirs(UPLOAD_DIR, exist_ok=True)


def get_db():
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()


def web_file_response(filename: str) -> FileResponse:
    response = FileResponse(os.path.join(WEB_DIR, filename))
    response.headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0"
    response.headers["Pragma"] = "no-cache"
    response.headers["Expires"] = "0"
    return response


def build_upload_url(request: Request, filename: str) -> str:
    return str(request.base_url).rstrip("/") + f"/uploads/{filename}"


def build_thumbnail_url(request: Request, filename: str) -> str:
    return str(request.base_url).rstrip("/") + f"/thumbnails/{filename}"


@app.get("/thumbnails/{filename}")
def get_upload_thumbnail(filename: str):
    file_path = os.path.join(UPLOAD_DIR, filename)
    if not os.path.isfile(file_path):
        raise HTTPException(status_code=404, detail="Không tìm thấy ảnh")

    try:
        with Image.open(file_path) as image:
            image = image.convert("RGB")
            image.thumbnail((THUMBNAIL_MAX_SIZE, THUMBNAIL_MAX_SIZE))
            buffer = BytesIO()
            image.save(buffer, format="JPEG", quality=80, optimize=True)
            return Response(content=buffer.getvalue(), media_type="image/jpeg")
    except Exception:
        raise HTTPException(status_code=500, detail="Không thể tạo thumbnail")


app.mount("/uploads", StaticFiles(directory=UPLOAD_DIR), name="uploads")


def hash_password(password: str) -> str:
    salt = secrets.token_hex(16)
    digest = hashlib.pbkdf2_hmac("sha256", password.encode("utf-8"), salt.encode("utf-8"), PBKDF2_ITERATIONS)
    encoded = base64.urlsafe_b64encode(digest).decode("utf-8").rstrip("=")
    return f"pbkdf2_sha256${PBKDF2_ITERATIONS}${salt}${encoded}"


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


def needs_password_rehash(password_hash: str) -> bool:
    return not password_hash.startswith("pbkdf2_sha256$")


def encode_token(data: dict) -> str:
    payload = base64.urlsafe_b64encode(json.dumps(data).encode("utf-8")).decode("utf-8").rstrip("=")
    signature = hmac.new(APP_SECRET.encode("utf-8"), payload.encode("utf-8"), hashlib.sha256).hexdigest()
    return f"{payload}.{signature}"


def create_session_cookie(user: User) -> str:
    return encode_token({
        "uid": user.id,
        "role": user.role.name,
        "exp": int((datetime.utcnow() + timedelta(hours=TOKEN_HOURS)).timestamp())
    })


def get_current_user_from_request(request: Request, db: Session) -> Optional[User]:
    token = request.cookies.get(SESSION_COOKIE)
    if not token:
        return None
    try:
        data = decode_token(token)
    except Exception:
        return None
    return db.query(User).options(joinedload(User.role)).filter(User.id == data["uid"], User.is_active == True).first()


def translate_text(text: str, lang_code: str) -> str:
    try:
        return GoogleTranslator(source="vi", target=lang_code).translate(text)
    except Exception:
        return ""


def get_language_map(db: Session) -> dict[str, Language]:
    return {item.code: item for item in db.query(Language).filter(Language.is_active == True).all()}


def serialize_owner_stall_for_request(request: Request, stall: Stall) -> dict:
    payload = serialize_owner_stall(stall)
    if stall.image_url:
        payload["image_url"] = str(request.base_url).rstrip("/") + f"/uploads/{stall.image_url}"
    payload["qr_code_value"] = build_stall_qr_code(stall.id)
    payload["qr_launch_url"] = str(request.base_url).rstrip("/") + f"/qr/resolve?code={payload['qr_code_value']}"
    return payload


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


def build_stall_qr_code(stall_id: int) -> str:
    payload = f"stall:{stall_id}"
    signature = hmac.new(APP_SECRET.encode("utf-8"), payload.encode("utf-8"), hashlib.sha256).hexdigest()[:16]
    return f"sfqr1.{stall_id}.{signature}"


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


def require_auth_page(request: Request, db: Session) -> User:
    user = get_current_user_from_request(request, db)
    if not user:
        raise HTTPException(status_code=401, detail='Bạn cần đăng nhập')
    return user


def require_role(user: User, role_name: str):
    if not user.role or user.role.name != role_name:
        raise HTTPException(status_code=403, detail='Không có quyền truy cập')


def normalize_email_input(value: Optional[str], *, required: bool = False) -> Optional[str]:
    normalized = (value or "").strip().lower()
    if not normalized:
        if required:
            raise HTTPException(status_code=400, detail="Vui lòng nhập email")
        return None

    if not re.fullmatch(r"^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$", normalized):
        raise HTTPException(status_code=400, detail="Email không đúng định dạng")
    return normalized


def validate_password_input(password: Optional[str], *, required: bool = False) -> Optional[str]:
    normalized = (password or "").strip()
    if not normalized:
        if required:
            raise HTTPException(status_code=400, detail="Vui lòng nhập mật khẩu")
        return None

    if len(normalized) < 8:
        raise HTTPException(status_code=400, detail="Mật khẩu phải có ít nhất 8 ký tự")
    return normalized


def count_words(text: Optional[str]) -> int:
    return len(re.findall(r"\b\w+\b", text or "", flags=re.UNICODE))


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


def get_home_redirect_for_user(user: Optional[User]) -> str:
    if not user or not user.role:
        return "/login"
    if user.role.name == "super_admin":
        return "/superadmin"
    return "/owner"


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


def build_specialty_translations(specialties: list[str]) -> dict[str, list[str]]:
    clean_specialties = [item.strip() for item in specialties if item and item.strip()]
    if not clean_specialties:
        return {}

    # Do not translate specialties at response time.
    # Runtime translation here makes /nearby and /search extremely slow because
    # every request fans out into multiple translator calls for every stall.
    # The app already has a local fallback translator for known specialties.
    return {"vi": clean_specialties}


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


def get_owner_any_stall(db: Session, user_id: int) -> Optional[Stall]:
    return (
        db.query(Stall)
        .options(joinedload(Stall.category), joinedload(Stall.translations).joinedload(StallTranslation.language))
        .filter(Stall.created_by_user_id == user_id, Stall.is_deleted == False)
        .order_by(Stall.id.desc())
        .first()
    )


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


def owner_is_waiting_for_initial_approval(db: Session, user_id: int) -> bool:
    stall = get_owner_any_stall(db, user_id)
    if not stall or stall.is_active:
        return False
    pending = get_owner_pending_request(db, user_id)
    return pending is not None


def translations_to_dict(stall: Stall) -> dict[str, str]:
    output = {}
    for item in stall.translations:
        if item.language and item.script_text:
            output[item.language.code] = item.script_text
    return output


def normalize_specialty_values(*values: Optional[str]) -> list[str]:
    items = []
    for value in values:
        cleaned = (value or "").strip()
        if cleaned:
            items.append(cleaned)
    return items[:3]


def require_specialties(*values: Optional[str]) -> tuple[str, str, str]:
    items = normalize_specialty_values(*values)
    if len(items) != 3:
        raise HTTPException(status_code=400, detail="Vui lòng nhập đủ 3 món đặc sản")
    return items[0], items[1], items[2]


def format_specialties_text(specialties: list[str]) -> str:
    items = [item.strip() for item in specialties if item and item.strip()]
    if not items:
        return "những món đặc trưng của quán"
    if len(items) == 1:
        return items[0]
    if len(items) == 2:
        return f"{items[0]} và {items[1]}"
    return f"{', '.join(items[:-1])} và {items[-1]}"


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


def require_poi_radius(value: Optional[float]) -> float:
    radius = float(value or 0)
    if radius < 10:
        raise HTTPException(status_code=400, detail="Vui lòng nhập bán kính POI tối thiểu 10m")
    return radius


def normalize_time_value(value: Optional[str]) -> str:
    cleaned = (value or "").strip()
    if not cleaned:
        return ""
    if not re.fullmatch(r"\d{2}:\d{2}", cleaned):
        raise HTTPException(status_code=400, detail="Vui lòng nhập giờ theo định dạng HH:MM")
    return cleaned


def build_opening_hours(opening_time: Optional[str], closing_time: Optional[str]) -> str:
    open_value = normalize_time_value(opening_time)
    close_value = normalize_time_value(closing_time)
    if not open_value or not close_value:
        raise HTTPException(status_code=400, detail="Vui lòng nhập đầy đủ giờ mở và giờ đóng")
    return f"{open_value} - {close_value}"


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


def split_opening_hours(opening_hours: Optional[str]) -> tuple[str, str]:
    raw = (opening_hours or "").strip()
    if not raw:
        return "", ""
    normalized = raw.replace("–", "-").replace("—", "-")
    parts = [part.strip() for part in normalized.split("-") if part.strip()]
    if len(parts) >= 2:
        return parts[0], parts[1]
    return raw, ""


def serialize_specialties(source) -> list[str]:
    return normalize_specialty_values(
        getattr(source, "specialty_1", ""),
        getattr(source, "specialty_2", ""),
        getattr(source, "specialty_3", "")
    )


def normalize_search_text(value: Optional[str]) -> str:
    raw = (value or "").strip().lower()
    if not raw:
        return ""
    normalized = unicodedata.normalize("NFD", raw)
    without_marks = "".join(ch for ch in normalized if unicodedata.category(ch) != "Mn")
    return without_marks.replace("đ", "d")


def split_search_terms(value: Optional[str]) -> list[str]:
    return [item for item in normalize_search_text(value).split() if item]


def format_distance_text(distance_km: float) -> str:
    if distance_km < 1:
        return f"{int(round(distance_km * 1000))}m"
    return f"{round(distance_km, 2)}km"


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

    # Some legacy stalls already carry an aggregate rating/count in `stalls`
    # but do not have matching rows in `reviews`. Preserve that baseline and
    # add real review rows on top so a new rating does not overwrite history.
    baseline_review_count = max(current_review_count - actual_review_count, 0)
    baseline_review_sum = max((current_rating_avg * current_review_count) - actual_review_sum, 0)

    combined_review_count = baseline_review_count + actual_review_count
    combined_review_sum = baseline_review_sum + actual_review_sum
    combined_rating_avg = (combined_review_sum / combined_review_count) if combined_review_count > 0 else 0

    stall.reviews_count = combined_review_count
    stall.rating_avg = round(combined_rating_avg, 1)
    stall.updated_at = datetime.utcnow()


def rounded_tenth(value: float) -> float:
    return math.floor((value * 10) + 0.5) / 10.0


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


def get_request_ip(request: Request) -> str:
    forwarded = request.headers.get("x-forwarded-for", "")
    if forwarded:
        first_hop = forwarded.split(",")[0].strip()
        if first_hop:
            return first_hop
    return request.client.host if request.client else ""


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


def get_location_log_user_key(row: "LocationLog") -> str:
    return row.device_id or row.session_id or f"anon:{row.id}"


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


def map_tts_language(language_code: str) -> str:
    return {
        "vi": "vi",
        "en": "en",
        "ja": "ja",
        "ko": "ko",
        "zh-CN": "zh-CN",
    }.get(language_code, "vi")


def generate_audio_bytes(script_text: str, language_code: str) -> bytes:
    audio_buffer = BytesIO()
    tts = gTTS(text=script_text, lang=map_tts_language(language_code))
    tts.write_to_fp(audio_buffer)
    return audio_buffer.getvalue()


def get_script_hash(script_text: str, language_code: str) -> str:
    content = f"{AUDIO_PROFILE_VERSION}:{language_code}:{script_text}".encode("utf-8")
    return hashlib.sha256(content).hexdigest()


def serialize_user(user: User) -> dict:
    return {
        "id": user.id,
        "full_name": user.full_name,
        "username": user.username,
        "email": user.email,
        "is_active": user.is_active,
        "role": user.role.name if user.role else None
    }


def serialize_admin_user(request: Request, user: User, stall: Optional[Stall] = None) -> dict:
    payload = serialize_user(user)
    payload["stall_name"] = stall.name if stall else ""
    payload["stall"] = serialize_owner_stall_for_request(request, stall) if stall else None
    return payload


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


def get_stall_script_vi(stall: Optional[Stall]) -> str:
    if not stall:
        return ""
    return translations_to_dict(stall).get("vi", "")


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


def ensure_stall_owner_assignments():
    # Legacy helper kept only so old scripts do not crash if they import it.
    # Current onboarding flow must never auto-create or auto-assign owners.
    return


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


class LoginRequest(BaseModel):
    username: str
    password: str


class CreateOwnerRequest(BaseModel):
    username: str
    password: str
    full_name: str
    email: Optional[str] = None


class UpdateUserRequest(BaseModel):
    username: str
    full_name: str
    email: Optional[str] = None
    password: Optional[str] = None


class CreateAdminNotificationRequest(BaseModel):
    title: str
    message: str
    recipient_scope: str = "selected_users"
    user_ids: list[int] = []


class UserLocation(BaseModel):
    lat: float
    lng: float


class SearchRequest(BaseModel):
    query: str
    lat: Optional[float] = None
    lng: Optional[float] = None
    limit: int = 20


class RatingRequest(BaseModel):
    rating: int
    lat: Optional[float] = None
    lng: Optional[float] = None


@app.get("/")
def root(request: Request, db: Session = Depends(get_db)):
    user = get_current_user_from_request(request, db)
    if not user:
        return RedirectResponse(url="/login", status_code=302)
    if user.role.name == "super_admin":
        return RedirectResponse(url="/superadmin", status_code=302)
    return RedirectResponse(url="/owner", status_code=302)


@app.get("/login")
def login_page(request: Request, db: Session = Depends(get_db)):
    user = get_current_user_from_request(request, db)
    if user:
        return RedirectResponse(url=get_home_redirect_for_user(user), status_code=302)
    return web_file_response("login.html")


@app.get("/owner")
def owner_page(request: Request, db: Session = Depends(get_db)):
    user = get_current_user_from_request(request, db)
    if not user:
        return RedirectResponse(url="/login", status_code=302)
    if not user.role or user.role.name != "stall_owner":
        return RedirectResponse(url=get_home_redirect_for_user(user), status_code=302)
    stall = get_owner_stall(db, user.id)
    page = "owner-dashboard.html" if stall else "admin.html"
    return web_file_response(page)


@app.get("/superadmin")
def superadmin_page(request: Request, db: Session = Depends(get_db)):
    user = get_current_user_from_request(request, db)
    if not user:
        return RedirectResponse(url="/login", status_code=302)
    if not user.role or user.role.name != "super_admin":
        return RedirectResponse(url=get_home_redirect_for_user(user), status_code=302)
    return web_file_response("superadmin.html")


@app.post("/auth/login")
def login(payload: LoginRequest, db: Session = Depends(get_db)):
    user = db.query(User).options(joinedload(User.role)).filter(User.username == payload.username).first()
    if not user or not verify_password(payload.password, user.password_hash):
        raise HTTPException(status_code=401, detail="Tên đăng nhập hoặc mật khẩu không đúng")

    if not user.is_active:
        if user.role and user.role.name == "stall_owner" and owner_is_waiting_for_initial_approval(db, user.id):
            raise HTTPException(status_code=403, detail="Tài khoản đang chờ admin duyệt gian hàng đầu tiên. Vui lòng đăng nhập lại sau khi được duyệt.")
        raise HTTPException(status_code=403, detail="Tài khoản hiện đang bị khóa")

    if needs_password_rehash(user.password_hash):
        user.password_hash = hash_password(payload.password)
        user.updated_at = datetime.utcnow()
        db.commit()
        db.refresh(user)

    response = JSONResponse({"user": serialize_user(user)})
    response.set_cookie(
        key=SESSION_COOKIE,
        value=create_session_cookie(user),
        httponly=True,
        samesite="lax",
        secure=COOKIE_SECURE,
        max_age=TOKEN_HOURS * 3600
    )
    return response


@app.post("/auth/logout")
def logout():
    response = JSONResponse({"status": "success"})
    response.delete_cookie(SESSION_COOKIE, samesite="lax", secure=COOKIE_SECURE)
    return response


@app.get("/auth/me")
def auth_me(request: Request, db: Session = Depends(get_db)):
    user = require_auth_page(request, db)
    owner_stall = get_owner_stall(db, user.id) if user.role.name == "stall_owner" else None
    return {
        "user": serialize_user(user),
        "has_stall": owner_stall is not None,
        "pending_initial_approval": owner_is_waiting_for_initial_approval(db, user.id) if user.role.name == "stall_owner" else False
    }


@app.get("/categories")
def categories(request: Request, db: Session = Depends(get_db)):
    items = db.query(Category).filter(Category.is_active == True).order_by(Category.id.asc()).all()
    return [{"id": item.id, "name": item.name} for item in items]


@app.get("/owner/stall")
def owner_stall(request: Request, db: Session = Depends(get_db)):
    user = require_auth_page(request, db)
    require_role(user, "stall_owner")
    stall = get_owner_stall(db, user.id)
    return {"stall": serialize_owner_stall_for_request(request, stall) if stall else None}


@app.get("/owner/dashboard")
def owner_dashboard(request: Request, db: Session = Depends(get_db)):
    user = require_auth_page(request, db)
    require_role(user, "stall_owner")

    stall = get_owner_stall(db, user.id)
    if not stall:
        return {"stall": None, "metrics": {}, "latest_request": None}

    now = datetime.utcnow()
    last_7_days = now - timedelta(days=7)
    last_30_days = now - timedelta(days=30)

    total_listens = db.query(func.count(ListeningLog.id)).filter(ListeningLog.stall_id == stall.id).scalar() or 0
    listens_7_days = (
        db.query(func.count(ListeningLog.id))
        .filter(ListeningLog.stall_id == stall.id, ListeningLog.listened_at >= last_7_days)
        .scalar() or 0
    )
    listens_30_days = (
        db.query(func.count(ListeningLog.id))
        .filter(ListeningLog.stall_id == stall.id, ListeningLog.listened_at >= last_30_days)
        .scalar() or 0
    )
    total_qr_scans = db.query(func.count(QrScanLog.id)).filter(QrScanLog.stall_id == stall.id).scalar() or 0
    qr_scans_7_days = (
        db.query(func.count(QrScanLog.id))
        .filter(QrScanLog.stall_id == stall.id, QrScanLog.scanned_at >= last_7_days)
        .scalar() or 0
    )
    qr_scans_30_days = (
        db.query(func.count(QrScanLog.id))
        .filter(QrScanLog.stall_id == stall.id, QrScanLog.scanned_at >= last_30_days)
        .scalar() or 0
    )
    average_listen_seconds = (
        db.query(func.avg(ListeningLog.duration_seconds))
        .filter(ListeningLog.stall_id == stall.id)
        .scalar() or 0
    )
    pending_requests = (
        db.query(func.count(StallUpdateRequest.id))
        .filter(StallUpdateRequest.stall_id == stall.id, StallUpdateRequest.status == "pending")
        .scalar() or 0
    )
    total_reviews = (
        db.query(func.count(Review.id))
        .filter(
            Review.stall_id == stall.id,
            Review.is_approved == True,
            Review.is_deleted == False
        )
        .scalar() or 0
    )
    rejected_requests = (
        db.query(func.count(StallUpdateRequest.id))
        .filter(StallUpdateRequest.stall_id == stall.id, StallUpdateRequest.status == "rejected")
        .scalar() or 0
    )

    latest_row = (
        db.query(StallUpdateRequest)
        .options(joinedload(StallUpdateRequest.stall).joinedload(Stall.category), joinedload(StallUpdateRequest.category))
        .filter(
            StallUpdateRequest.stall_id == stall.id,
            StallUpdateRequest.owner_deleted == False
        )
        .order_by(StallUpdateRequest.submitted_at.desc(), StallUpdateRequest.id.desc())
        .first()
    )

    trend_rows = (
        db.query(func.date(ListeningLog.listened_at).label("day"), func.count(ListeningLog.id).label("count"))
        .filter(ListeningLog.stall_id == stall.id, ListeningLog.listened_at >= last_7_days)
        .group_by(func.date(ListeningLog.listened_at))
        .order_by(func.date(ListeningLog.listened_at).asc())
        .all()
    )
    trend_map = {str(row.day): int(row.count or 0) for row in trend_rows}
    listens_trend = []
    for offset in range(6, -1, -1):
        day = (now - timedelta(days=offset)).date()
        listens_trend.append({
            "date": day.isoformat(),
            "label": day.strftime("%d/%m"),
            "count": trend_map.get(day.isoformat(), 0),
        })

    return {
        "stall": serialize_owner_stall_for_request(request, stall),
        "metrics": {
            "total_listens": int(total_listens),
            "listens_7_days": int(listens_7_days),
            "listens_30_days": int(listens_30_days),
            "total_qr_scans": int(total_qr_scans),
            "qr_scans_7_days": int(qr_scans_7_days),
            "qr_scans_30_days": int(qr_scans_30_days),
            "average_listen_seconds": round(float(average_listen_seconds or 0), 1),
            "total_reviews": int(total_reviews),
            "pending_requests": int(pending_requests),
            "rejected_requests": int(rejected_requests),
        },
        "listens_trend": listens_trend,
        "anti_spam_rules": {
            "listening_window_seconds": LISTEN_DEDUP_WINDOW_SECONDS,
            "qr_window_seconds": QR_DEDUP_WINDOW_SECONDS,
        },
        "latest_request": serialize_update_request_detail(request, latest_row) if latest_row else None,
    }


@app.get("/owner/update-requests")
def owner_update_requests(request: Request, db: Session = Depends(get_db)):
    user = require_auth_page(request, db)
    require_role(user, "stall_owner")

    stall = get_owner_stall(db, user.id)
    if not stall:
        return {"items": []}

    rows = (
        db.query(StallUpdateRequest)
        .options(joinedload(StallUpdateRequest.stall).joinedload(Stall.category), joinedload(StallUpdateRequest.category))
        .filter(
            StallUpdateRequest.stall_id == stall.id,
            StallUpdateRequest.owner_deleted == False
        )
        .order_by(StallUpdateRequest.submitted_at.desc(), StallUpdateRequest.id.desc())
        .limit(10)
        .all()
    )

    return {"items": [serialize_update_request_detail(request, row) for row in rows]}


@app.get("/owner/notifications")
def owner_notifications(request: Request, db: Session = Depends(get_db)):
    user = require_auth_page(request, db)
    require_role(user, "stall_owner")

    stall = get_owner_stall(db, user.id)
    update_request_items = []
    if stall:
        update_rows = (
            db.query(StallUpdateRequest)
            .options(joinedload(StallUpdateRequest.stall).joinedload(Stall.category), joinedload(StallUpdateRequest.category))
            .filter(
                StallUpdateRequest.stall_id == stall.id,
                StallUpdateRequest.owner_deleted == False
            )
            .order_by(StallUpdateRequest.submitted_at.desc(), StallUpdateRequest.id.desc())
            .limit(20)
            .all()
        )
        update_request_items = [serialize_owner_request_notification(request, row) for row in update_rows]

    admin_rows = (
        db.query(AdminNotificationRecipient)
        .options(joinedload(AdminNotificationRecipient.notification))
        .filter(
            AdminNotificationRecipient.user_id == user.id,
            AdminNotificationRecipient.deleted == False
        )
        .order_by(AdminNotificationRecipient.created_at.desc(), AdminNotificationRecipient.id.desc())
        .limit(20)
        .all()
    )
    admin_items = [serialize_owner_admin_notification(row) for row in admin_rows]

    items = update_request_items + admin_items
    items.sort(key=lambda item: item.get("created_at") or "", reverse=True)
    return {"items": items[:30]}


@app.post("/owner/update-requests/{request_id}/read")
def owner_mark_update_request_read(request_id: int, request: Request, db: Session = Depends(get_db)):
    user = require_auth_page(request, db)
    require_role(user, "stall_owner")

    stall = get_owner_stall(db, user.id)
    if not stall:
        raise HTTPException(status_code=404, detail="Bạn chưa có gian hàng")

    row = (
        db.query(StallUpdateRequest)
        .filter(
            StallUpdateRequest.id == request_id,
            StallUpdateRequest.stall_id == stall.id,
            StallUpdateRequest.owner_deleted == False
        )
        .first()
    )
    if not row:
        raise HTTPException(status_code=404, detail="Không tìm thấy thông báo")

    if row.owner_read_at is None:
        row.owner_read_at = datetime.utcnow()
        db.commit()

    return {"status": "success"}


@app.post("/owner/admin-notifications/{recipient_id}/read")
def owner_mark_admin_notification_read(recipient_id: int, request: Request, db: Session = Depends(get_db)):
    user = require_auth_page(request, db)
    require_role(user, "stall_owner")

    row = (
        db.query(AdminNotificationRecipient)
        .filter(
            AdminNotificationRecipient.id == recipient_id,
            AdminNotificationRecipient.user_id == user.id,
            AdminNotificationRecipient.deleted == False
        )
        .first()
    )
    if not row:
        raise HTTPException(status_code=404, detail="Không tìm thấy thông báo")

    if row.read_at is None:
        row.read_at = datetime.utcnow()
        row.updated_at = datetime.utcnow()
        db.commit()

    return {"status": "success"}


@app.delete("/owner/update-requests/{request_id}")
def owner_delete_update_request(request_id: int, request: Request, db: Session = Depends(get_db)):
    user = require_auth_page(request, db)
    require_role(user, "stall_owner")

    stall = get_owner_stall(db, user.id)
    if not stall:
        raise HTTPException(status_code=404, detail="Bạn chưa có gian hàng")

    row = (
        db.query(StallUpdateRequest)
        .filter(
            StallUpdateRequest.id == request_id,
            StallUpdateRequest.stall_id == stall.id,
            StallUpdateRequest.owner_deleted == False
        )
        .first()
    )
    if not row:
        raise HTTPException(status_code=404, detail="Không tìm thấy thông báo")

    row.owner_deleted = True
    if row.owner_read_at is None:
        row.owner_read_at = datetime.utcnow()
    db.commit()

    return {"status": "success"}


@app.delete("/owner/admin-notifications/{recipient_id}")
def owner_delete_admin_notification(recipient_id: int, request: Request, db: Session = Depends(get_db)):
    user = require_auth_page(request, db)
    require_role(user, "stall_owner")

    row = (
        db.query(AdminNotificationRecipient)
        .filter(
            AdminNotificationRecipient.id == recipient_id,
            AdminNotificationRecipient.user_id == user.id,
            AdminNotificationRecipient.deleted == False
        )
        .first()
    )
    if not row:
        raise HTTPException(status_code=404, detail="Không tìm thấy thông báo")

    row.deleted = True
    if row.read_at is None:
        row.read_at = datetime.utcnow()
    row.updated_at = datetime.utcnow()
    db.commit()
    return {"status": "success"}


@app.delete("/owner/update-requests")
def owner_delete_all_update_requests(request: Request, db: Session = Depends(get_db)):
    user = require_auth_page(request, db)
    require_role(user, "stall_owner")

    stall = get_owner_stall(db, user.id)
    if not stall:
        raise HTTPException(status_code=404, detail="Bạn chưa có gian hàng")

    rows = (
        db.query(StallUpdateRequest)
        .filter(
            StallUpdateRequest.stall_id == stall.id,
            StallUpdateRequest.owner_deleted == False
        )
        .all()
    )

    for row in rows:
        row.owner_deleted = True
        if row.owner_read_at is None:
            row.owner_read_at = datetime.utcnow()

    db.commit()
    return {"status": "success", "deleted_count": len(rows)}


@app.delete("/owner/admin-notifications")
def owner_delete_all_admin_notifications(request: Request, db: Session = Depends(get_db)):
    user = require_auth_page(request, db)
    require_role(user, "stall_owner")

    rows = (
        db.query(AdminNotificationRecipient)
        .filter(
            AdminNotificationRecipient.user_id == user.id,
            AdminNotificationRecipient.deleted == False
        )
        .all()
    )

    for row in rows:
        row.deleted = True
        if row.read_at is None:
            row.read_at = datetime.utcnow()
        row.updated_at = datetime.utcnow()

    db.commit()
    return {"status": "success", "deleted_count": len(rows)}


@app.post("/owner/stall")
async def owner_create_stall(
    request: Request,
    name: str = Form(...),
    lat: float = Form(...),
    lng: float = Form(...),
    category_id: int = Form(...),
    specialty_1: str = Form(...),
    specialty_2: str = Form(...),
    specialty_3: str = Form(...),
    poi_radius_m: float = Form(...),
    script_vi: str = Form(...),
    opening_time: str = Form(...),
    closing_time: str = Form(...),
    image: UploadFile = File(None),
    db: Session = Depends(get_db)
):
    user = require_auth_page(request, db)
    require_role(user, "stall_owner")
    script_vi = require_minimum_stall_script(script_vi)

    existing_active_stall = get_owner_stall(db, user.id)
    if existing_active_stall:
        raise HTTPException(status_code=400, detail="Bạn đã có gian hàng, hãy dùng chức năng cập nhật")

    existing_any_stall = get_owner_any_stall(db, user.id)
    if existing_any_stall and owner_is_waiting_for_initial_approval(db, user.id):
        raise HTTPException(status_code=400, detail="Bạn đã gửi yêu cầu tạo gian hàng và đang chờ admin duyệt")

    specialty_1, specialty_2, specialty_3 = require_specialties(specialty_1, specialty_2, specialty_3)
    poi_radius_m = require_poi_radius(poi_radius_m)
    opening_hours = build_opening_hours(opening_time, closing_time)

    filename = ""
    if image:
        ext = os.path.splitext(image.filename)[1]
        filename = f"{uuid.uuid4()}{ext}"
        with open(os.path.join(UPLOAD_DIR, filename), "wb") as buffer:
            shutil.copyfileobj(image.file, buffer)

    if existing_any_stall:
        stall = existing_any_stall
        stall.category_id = category_id
        stall.name = name
        stall.latitude = lat
        stall.longitude = lng
        stall.image_url = filename
        stall.specialty_1 = specialty_1
        stall.specialty_2 = specialty_2
        stall.specialty_3 = specialty_3
        stall.poi_radius_m = poi_radius_m
        stall.opening_hours = opening_hours
        stall.is_open = True
        stall.is_active = False
        stall.is_deleted = False
        stall.updated_at = datetime.utcnow()
    else:
        stall = Stall(
            category_id=category_id,
            name=name,
            latitude=lat,
            longitude=lng,
            image_url=filename,
            specialty_1=specialty_1,
            specialty_2=specialty_2,
            specialty_3=specialty_3,
            poi_radius_m=poi_radius_m,
            opening_hours=opening_hours,
            is_open=True,
            is_active=False,
            rating_avg=0,
            reviews_count=0,
            created_by_user_id=user.id
        )
        db.add(stall)
        db.flush()

    upsert_stall_translations(db, stall, name, script_vi)

    pending = (
        db.query(StallUpdateRequest)
        .filter(StallUpdateRequest.stall_id == stall.id, StallUpdateRequest.status == "pending")
        .order_by(StallUpdateRequest.id.desc())
        .first()
    )

    if pending:
        pending.category_id = category_id
        pending.name = name
        pending.latitude = lat
        pending.longitude = lng
        pending.specialty_1 = specialty_1
        pending.specialty_2 = specialty_2
        pending.specialty_3 = specialty_3
        pending.poi_radius_m = poi_radius_m
        pending.opening_hours = opening_hours
        pending.is_open = True
        pending.script_vi = script_vi
        pending.image_url = filename
        pending.submitted_at = datetime.utcnow()
    else:
        db.add(StallUpdateRequest(
            stall_id=stall.id,
            submitted_by_user_id=user.id,
            category_id=category_id,
            name=name,
            latitude=lat,
            longitude=lng,
            specialty_1=specialty_1,
            specialty_2=specialty_2,
            specialty_3=specialty_3,
            poi_radius_m=poi_radius_m,
            opening_hours=opening_hours,
            is_open=True,
            script_vi=script_vi,
            image_url=filename,
            status="pending"
        ))

    user.is_active = False
    user.updated_at = datetime.utcnow()
    db.commit()
    return {"status": "success", "stall_id": stall.id, "pending_review": True}


@app.post("/owner/stall-update-request")
async def owner_update_request(
    request: Request,
    name: str = Form(...),
    lat: float = Form(...),
    lng: float = Form(...),
    category_id: int = Form(...),
    specialty_1: str = Form(...),
    specialty_2: str = Form(...),
    specialty_3: str = Form(...),
    poi_radius_m: float = Form(...),
    script_vi: str = Form(...),
    opening_time: str = Form(...),
    closing_time: str = Form(...),
    is_open: bool = Form(True),
    image: UploadFile = File(None),
    db: Session = Depends(get_db)
):
    user = require_auth_page(request, db)
    require_role(user, "stall_owner")
    script_vi = require_minimum_stall_script(script_vi)

    stall = get_owner_stall(db, user.id)
    if not stall:
        raise HTTPException(status_code=404, detail="Bạn chưa có gian hàng")

    specialty_1, specialty_2, specialty_3 = require_specialties(specialty_1, specialty_2, specialty_3)
    poi_radius_m = require_poi_radius(poi_radius_m)
    opening_hours = build_opening_hours(opening_time, closing_time)

    filename = stall.image_url
    if image:
        ext = os.path.splitext(image.filename)[1]
        filename = f"{uuid.uuid4()}{ext}"
        with open(os.path.join(UPLOAD_DIR, filename), "wb") as buffer:
            shutil.copyfileobj(image.file, buffer)

    pending = (
        db.query(StallUpdateRequest)
        .filter(StallUpdateRequest.stall_id == stall.id, StallUpdateRequest.status == "pending")
        .order_by(StallUpdateRequest.id.desc())
        .first()
    )

    if pending:
        pending.category_id = category_id
        pending.name = name
        pending.latitude = lat
        pending.longitude = lng
        pending.specialty_1 = specialty_1
        pending.specialty_2 = specialty_2
        pending.specialty_3 = specialty_3
        pending.poi_radius_m = poi_radius_m
        pending.opening_hours = opening_hours
        pending.is_open = is_open
        pending.script_vi = script_vi
        pending.image_url = filename
        pending.submitted_at = datetime.utcnow()
    else:
        db.add(StallUpdateRequest(
            stall_id=stall.id,
            submitted_by_user_id=user.id,
            category_id=category_id,
            name=name,
            latitude=lat,
            longitude=lng,
            specialty_1=specialty_1,
            specialty_2=specialty_2,
            specialty_3=specialty_3,
            poi_radius_m=poi_radius_m,
            opening_hours=opening_hours,
            is_open=is_open,
            script_vi=script_vi,
            image_url=filename,
            status="pending"
        ))

    db.commit()
    return {"status": "success"}


@app.get("/admin/dashboard")
def admin_dashboard(request: Request, db: Session = Depends(get_db)):
    user = require_auth_page(request, db)
    require_role(user, "super_admin")

    owner_role = db.query(Role).filter(Role.name == "stall_owner").first()
    owner_role_id = owner_role.id if owner_role else -1

    total_stalls = db.query(func.count(Stall.id)).filter(Stall.is_deleted == False).scalar() or 0
    active_stalls = db.query(func.count(Stall.id)).filter(Stall.is_deleted == False, Stall.is_active == True).scalar() or 0
    total_owners = db.query(func.count(User.id)).filter(User.role_id == owner_role_id).scalar() or 0
    total_listens = db.query(func.count(ListeningLog.id)).scalar() or 0
    pending_updates = db.query(func.count(StallUpdateRequest.id)).filter(StallUpdateRequest.status == "pending").scalar() or 0
    average_listen_seconds = db.query(func.avg(ListeningLog.duration_seconds)).scalar() or 0
    unique_sessions = db.query(func.count(func.distinct(ListeningLog.session_id))).scalar() or 0
    unique_devices = db.query(func.count(func.distinct(ListeningLog.device_id))).scalar() or 0
    active_since = datetime.utcnow() - timedelta(minutes=5)

    recent_location_rows = (
        db.query(LocationLog)
        .filter(LocationLog.recorded_at >= active_since)
        .order_by(LocationLog.recorded_at.desc())
        .limit(2000)
        .all()
    )

    active_user_keys = {
        get_location_log_user_key(row)
        for row in recent_location_rows
        if row.latitude is not None and row.longitude is not None
    }

    latest_positions_by_user = {}
    for row in recent_location_rows:
        if row.latitude is None or row.longitude is None:
            continue

        user_key = get_location_log_user_key(row)
        if user_key in latest_positions_by_user:
            continue

        latest_positions_by_user[user_key] = {
            "user_key": user_key,
            "lat": float(row.latitude),
            "lng": float(row.longitude),
            "source": row.source or "app",
            "recorded_at": row.recorded_at.isoformat() if row.recorded_at else None
        }

    top_rows = (
        db.query(
            Stall.name,
            func.count(ListeningLog.id).label("listens"),
            func.avg(ListeningLog.duration_seconds).label("avg_duration")
        )
        .join(ListeningLog, ListeningLog.stall_id == Stall.id)
        .group_by(Stall.id)
        .order_by(func.count(ListeningLog.id).desc())
        .limit(5)
        .all()
    )

    listening_stats_subquery = (
        db.query(
            ListeningLog.stall_id.label("stall_id"),
            func.count(ListeningLog.id).label("listens"),
            func.avg(ListeningLog.duration_seconds).label("avg_duration")
        )
        .group_by(ListeningLog.stall_id)
        .subquery()
    )

    qr_stats_subquery = (
        db.query(
            QrScanLog.stall_id.label("stall_id"),
            func.count(QrScanLog.id).label("qr_scans")
        )
        .group_by(QrScanLog.stall_id)
        .subquery()
    )

    poi_rows = (
        db.query(
            Stall.name,
            User.full_name,
            User.username,
            func.coalesce(listening_stats_subquery.c.listens, 0).label("listens"),
            func.coalesce(qr_stats_subquery.c.qr_scans, 0).label("qr_scans"),
            func.coalesce(listening_stats_subquery.c.avg_duration, 0).label("avg_duration")
        )
        .outerjoin(User, Stall.created_by_user_id == User.id)
        .outerjoin(listening_stats_subquery, listening_stats_subquery.c.stall_id == Stall.id)
        .outerjoin(qr_stats_subquery, qr_stats_subquery.c.stall_id == Stall.id)
        .filter(Stall.is_deleted == False)
        .order_by(
            func.coalesce(listening_stats_subquery.c.listens, 0).desc(),
            func.coalesce(qr_stats_subquery.c.qr_scans, 0).desc(),
            Stall.name.asc()
        )
        .limit(100)
        .all()
    )

    heatmap_groups = {}
    for row in recent_location_rows:
        if row.latitude is None or row.longitude is None:
            continue

        lat_key = round(float(row.latitude), 4)
        lng_key = round(float(row.longitude), 4)
        group_key = (lat_key, lng_key)
        device_key = get_location_log_user_key(row)
        bucket = heatmap_groups.setdefault(group_key, {"lat": lat_key, "lng": lng_key, "users": set(), "hits": 0})
        bucket["users"].add(device_key)
        bucket["hits"] += 1

    return {
        "metrics": {
            "total_stalls": total_stalls,
            "active_stalls": active_stalls,
            "total_owners": total_owners,
            "total_listens": total_listens*2,
            "pending_updates": pending_updates,
            "average_listen_seconds": round(float(average_listen_seconds or 0), 1),
            "unique_sessions": int(unique_sessions or 0),
            "unique_devices": int(unique_devices or 0),
            "active_users_5m": len(active_user_keys) #kiểm tra số lượng người online
        },
        "top_stalls": [{"name": row[0], "listens": row[1], "avg_duration": round(float(row[2] or 0), 1)} for row in top_rows],
        "poi_stats": [
            {
                "name": row[0],
                "owner_name": ((row[1] or "").strip() or row[2] or "Chưa gán owner"),
                "listens": int(row[3] or 0),
                "qr_scans": int(row[4] or 0),
                "avg_duration": round(float(row[5] or 0), 1),
            }
            for row in poi_rows
        ],
        "anti_spam_rules": {
            "listening_window_seconds": LISTEN_DEDUP_WINDOW_SECONDS,
            "qr_window_seconds": QR_DEDUP_WINDOW_SECONDS,
        },
        "heatmap_points": sorted(
            [
                {
                    "lat": point["lat"],
                    "lng": point["lng"],
                    "users": len(point["users"]),
                    "hits": point["hits"]
                }
                for point in heatmap_groups.values()
                if point["users"]
            ],
            key=lambda item: (item["users"], item["hits"]),
            reverse=True
        )[:300],
        "active_user_positions": list(latest_positions_by_user.values())[:500]
    }


@app.get("/admin/users")
def admin_users(request: Request, db: Session = Depends(get_db)):
    user = require_auth_page(request, db)
    require_role(user, "super_admin")

    users = (
        db.query(User)
        .options(joinedload(User.role))
        .join(Role, User.role_id == Role.id)
        .filter(Role.name == "stall_owner")
        .order_by(User.id.asc())
        .all()
    )
    return {
        "users": [
            serialize_admin_user(request, item, get_owner_stall(db, item.id))
            for item in users
        ]
    }


@app.get("/admin/users/{user_id}")
def admin_user_detail(user_id: int, request: Request, db: Session = Depends(get_db)):
    user = require_auth_page(request, db)
    require_role(user, "super_admin")

    target_user = db.query(User).options(joinedload(User.role)).filter(User.id == user_id).first()
    if not target_user:
        raise HTTPException(status_code=404, detail="Không tìm thấy người dùng")

    stall = get_owner_stall(db, target_user.id) if target_user.role and target_user.role.name == "stall_owner" else None
    return {"user": serialize_admin_user(request, target_user, stall)}


@app.post("/admin/users")
def admin_create_owner(payload: CreateOwnerRequest, request: Request, db: Session = Depends(get_db)):
    user = require_auth_page(request, db)
    require_role(user, "super_admin")

    username = payload.username.strip()
    full_name = payload.full_name.strip()
    email = normalize_email_input(payload.email, required=True)
    password = validate_password_input(payload.password, required=True)

    if not username:
        raise HTTPException(status_code=400, detail="Vui lòng nhập tên đăng nhập")
    if not full_name:
        raise HTTPException(status_code=400, detail="Vui lòng nhập họ tên")

    role = db.query(Role).filter(Role.name == "stall_owner").first()
    if not role:
        raise HTTPException(status_code=400, detail="Không tìm thấy vai trò chủ gian hàng")

    if db.query(User).filter(User.username == username).first():
        raise HTTPException(status_code=400, detail="Tên đăng nhập đã tồn tại")
    if db.query(User).filter(User.email == email).first():
        raise HTTPException(status_code=400, detail="Email đã tồn tại")

    db_user = User(
        role_id=role.id,
        username=username,
        password_hash=hash_password(password),
        full_name=full_name,
        email=email,
        is_active=True
    )
    db.add(db_user)
    db.commit()
    db.refresh(db_user)
    return {"status": "success", "user": serialize_user(db_user)}


@app.get("/admin/notification-recipients")
def admin_notification_recipients(request: Request, db: Session = Depends(get_db)):
    user = require_auth_page(request, db)
    require_role(user, "super_admin")

    owner_role = db.query(Role).filter(Role.name == "stall_owner").first()
    if not owner_role:
        return {"items": [], "summary": {"all_owners": 0, "active_owners": 0}}

    users = (
        db.query(User)
        .options(joinedload(User.role))
        .filter(User.role_id == owner_role.id)
        .order_by(User.full_name.asc(), User.username.asc())
        .all()
    )
    owner_ids = [item.id for item in users]
    stalls = db.query(Stall).filter(Stall.created_by_user_id.in_(owner_ids or [-1])).all()
    stall_map = {item.created_by_user_id: item for item in stalls}
    items = [serialize_admin_notification_recipient(request, item, stall_map.get(item.id)) for item in users]
    return {
        "items": items,
        "summary": {
            "all_owners": len(items),
            "active_owners": sum(1 for item in items if item["is_active"]),
        }
    }


@app.post("/admin/notifications")
def admin_create_notification(payload: CreateAdminNotificationRequest, request: Request, db: Session = Depends(get_db)):
    user = require_auth_page(request, db)
    require_role(user, "super_admin")

    title = payload.title.strip()
    message = payload.message.strip()
    if not title:
        raise HTTPException(status_code=400, detail="Vui lòng nhập tiêu đề thông báo")
    if not message:
        raise HTTPException(status_code=400, detail="Vui lòng nhập nội dung thông báo")

    recipients = get_notification_recipient_users(db, payload.recipient_scope, payload.user_ids)
    if not recipients:
        raise HTTPException(status_code=400, detail="Không có người nhận phù hợp")

    now = datetime.utcnow()
    notification = AdminNotification(
        title=title,
        message=message,
        recipient_scope=payload.recipient_scope,
        created_by_user_id=user.id,
        created_at=now,
        updated_at=now,
    )
    db.add(notification)
    db.flush()

    for recipient in recipients:
        db.add(AdminNotificationRecipient(
            notification_id=notification.id,
            user_id=recipient.id,
            created_at=now,
            updated_at=now,
        ))

    db.commit()
    return {"status": "success", "notification_id": notification.id, "recipient_count": len(recipients)}


@app.get("/admin/notifications")
def admin_notifications(request: Request, db: Session = Depends(get_db)):
    user = require_auth_page(request, db)
    require_role(user, "super_admin")

    rows = (
        db.query(AdminNotification)
        .options(joinedload(AdminNotification.creator))
        .order_by(AdminNotification.created_at.desc(), AdminNotification.id.desc())
        .limit(50)
        .all()
    )
    notification_ids = [row.id for row in rows]
    recipient_rows = (
        db.query(AdminNotificationRecipient)
        .options(joinedload(AdminNotificationRecipient.user))
        .filter(AdminNotificationRecipient.notification_id.in_(notification_ids or [-1]))
        .order_by(AdminNotificationRecipient.id.asc())
        .all()
    )
    grouped: dict[int, list[AdminNotificationRecipient]] = {}
    for item in recipient_rows:
        grouped.setdefault(item.notification_id, []).append(item)

    return {"items": [serialize_admin_notification_history(row, grouped.get(row.id, [])) for row in rows]}


@app.put("/admin/users/{user_id}")
def admin_update_user(user_id: int, payload: UpdateUserRequest, request: Request, db: Session = Depends(get_db)):
    user = require_auth_page(request, db)
    require_role(user, "super_admin")

    target_user = db.query(User).options(joinedload(User.role)).filter(User.id == user_id).first()
    if not target_user:
        raise HTTPException(status_code=404, detail="Khong tim thay nguoi dung")

    username = payload.username.strip()
    full_name = payload.full_name.strip()
    email = normalize_email_input(payload.email, required=False)
    password = validate_password_input(payload.password, required=False)

    if not username:
        raise HTTPException(status_code=400, detail="Vui lòng nhập tên đăng nhập")
    if not full_name:
        raise HTTPException(status_code=400, detail="Vui lòng nhập họ tên")

    same_username = db.query(User).filter(User.username == username, User.id != user_id).first()
    if same_username:
        raise HTTPException(status_code=400, detail="Ten dang nhap da ton tai")

    if email:
        same_email = db.query(User).filter(User.email == email, User.id != user_id).first()
        if same_email:
            raise HTTPException(status_code=400, detail="Email da ton tai")

    target_user.username = username
    target_user.full_name = full_name
    target_user.email = email
    target_user.updated_at = datetime.utcnow()

    if password:
        target_user.password_hash = hash_password(password)

    db.commit()
    db.refresh(target_user)
    return {"status": "success", "user": serialize_user(target_user)}


@app.post("/admin/users/{user_id}/manage")
async def admin_manage_user(
    user_id: int,
    request: Request,
    full_name: str = Form(...),
    username: str = Form(...),
    email: str = Form(""),
    password: str = Form(""),
    stall_name: str = Form(""),
    category_id: Optional[int] = Form(None),
    lat: Optional[float] = Form(None),
    lng: Optional[float] = Form(None),
    specialty_1: str = Form(""),
    specialty_2: str = Form(""),
    specialty_3: str = Form(""),
    poi_radius_m: Optional[float] = Form(None),
    script_vi: str = Form(""),
    opening_time: str = Form(""),
    closing_time: str = Form(""),
    opening_hours: str = Form(""),
    is_open: bool = Form(True),
    image: UploadFile = File(None),
    db: Session = Depends(get_db)
):
    user = require_auth_page(request, db)
    require_role(user, "super_admin")

    target_user = db.query(User).options(joinedload(User.role)).filter(User.id == user_id).first()
    if not target_user:
        raise HTTPException(status_code=404, detail="Không tìm thấy người dùng")

    normalized_username = username.strip()
    normalized_full_name = full_name.strip()
    normalized_email = normalize_email_input(email, required=False)
    normalized_password = validate_password_input(password, required=False)

    if not normalized_username:
        raise HTTPException(status_code=400, detail="Vui lòng nhập tên đăng nhập")
    if not normalized_full_name:
        raise HTTPException(status_code=400, detail="Vui lòng nhập họ tên")

    same_username = db.query(User).filter(User.username == normalized_username, User.id != user_id).first()
    if same_username:
        raise HTTPException(status_code=400, detail="Tên đăng nhập đã tồn tại")

    if normalized_email:
        same_email = db.query(User).filter(User.email == normalized_email, User.id != user_id).first()
        if same_email:
            raise HTTPException(status_code=400, detail="Email đã tồn tại")

    target_user.full_name = normalized_full_name
    target_user.username = normalized_username
    target_user.email = normalized_email
    target_user.updated_at = datetime.utcnow()

    if normalized_password:
        target_user.password_hash = hash_password(normalized_password)

    stall = None
    if target_user.role and target_user.role.name == "stall_owner":
        stall = get_owner_stall(db, target_user.id)
        if stall:
            clean_name = stall_name.strip()
            clean_script = require_minimum_stall_script(script_vi)
            if not clean_name:
                raise HTTPException(status_code=400, detail="Vui lòng nhập tên gian hàng")
            if category_id is None:
                raise HTTPException(status_code=400, detail="Vui lòng chọn danh mục")
            if lat is None or lng is None:
                raise HTTPException(status_code=400, detail="Vui lòng nhập đầy đủ tọa độ")

            specialty_1, specialty_2, specialty_3 = require_specialties(specialty_1, specialty_2, specialty_3)
            poi_radius_m = require_poi_radius(poi_radius_m)
            opening_hours = resolve_opening_hours_input(opening_time, closing_time, opening_hours)

            filename = stall.image_url
            if image and image.filename:
                ext = os.path.splitext(image.filename)[1]
                filename = f"{uuid.uuid4()}{ext}"
                with open(os.path.join(UPLOAD_DIR, filename), "wb") as buffer:
                    shutil.copyfileobj(image.file, buffer)

            stall.name = clean_name
            stall.category_id = category_id
            stall.latitude = lat
            stall.longitude = lng
            stall.specialty_1 = specialty_1
            stall.specialty_2 = specialty_2
            stall.specialty_3 = specialty_3
            stall.poi_radius_m = poi_radius_m
            stall.opening_hours = opening_hours
            stall.is_open = is_open
            stall.image_url = filename
            stall.updated_at = datetime.utcnow()
            upsert_stall_translations(db, stall, clean_name, clean_script)

    db.commit()
    db.refresh(target_user)
    return {"status": "success", "user": serialize_admin_user(request, target_user, stall)}


@app.patch("/admin/users/{user_id}/hide")
def admin_hide_user(user_id: int, request: Request, db: Session = Depends(get_db)):
    user = require_auth_page(request, db)
    require_role(user, "super_admin")

    target_user = db.query(User).options(joinedload(User.role)).filter(User.id == user_id).first()
    if not target_user:
        raise HTTPException(status_code=404, detail="Khong tim thay nguoi dung")
    if not target_user.role or target_user.role.name != "stall_owner":
        raise HTTPException(status_code=400, detail="Chi co the an chu gian hang")

    target_user.is_active = False
    target_user.updated_at = datetime.utcnow()

    owner_stalls = db.query(Stall).filter(
        Stall.created_by_user_id == target_user.id,
        Stall.is_deleted == False
    ).all()
    for stall in owner_stalls:
        stall.is_active = False
        stall.updated_at = datetime.utcnow()

    db.commit()
    return {"status": "success"}


@app.patch("/admin/users/{user_id}/activate")
def admin_activate_user(user_id: int, request: Request, db: Session = Depends(get_db)):
    user = require_auth_page(request, db)
    require_role(user, "super_admin")

    target_user = db.query(User).options(joinedload(User.role)).filter(User.id == user_id).first()
    if not target_user:
        raise HTTPException(status_code=404, detail="Khong tim thay nguoi dung")
    if not target_user.role or target_user.role.name != "stall_owner":
        raise HTTPException(status_code=400, detail="Chi co the khoi phuc chu gian hang")

    target_user.is_active = True
    target_user.updated_at = datetime.utcnow()

    owner_stalls = db.query(Stall).filter(
        Stall.created_by_user_id == target_user.id,
        Stall.is_deleted == False
    ).all()
    for stall in owner_stalls:
        stall.is_active = True
        stall.updated_at = datetime.utcnow()

    db.commit()
    return {"status": "success"}


@app.get("/admin/update-requests")
def admin_update_requests(request: Request, db: Session = Depends(get_db)):
    user = require_auth_page(request, db)
    require_role(user, "super_admin")

    rows = (
        db.query(StallUpdateRequest)
        .options(joinedload(StallUpdateRequest.stall).joinedload(Stall.category), joinedload(StallUpdateRequest.category))
        .filter(StallUpdateRequest.status == "pending")
        .order_by(StallUpdateRequest.submitted_at.asc())
        .all()
    )

    return {
        "items": [
            {
                "id": row.id,
                "stall_id": row.stall_id,
                "stall_name": row.stall.name if row.stall else "",
                "name": row.name,
                "specialties": serialize_specialties(row),
                "poi_radius_m": row.poi_radius_m or 30,
                "opening_hours": row.opening_hours or "",
                "is_open": row.is_open,
                "script_vi": row.script_vi,
                "submitted_at": row.submitted_at.isoformat(),
                "has_changes": any(item["changed"] for item in build_request_field_changes(
                    serialize_stall_for_compare(request, row.stall),
                    serialize_update_request_new_values(request, row)
                ))
            }
            for row in rows
        ]
    }


@app.get("/admin/update-requests/{request_id}")
def admin_update_request_detail(request_id: int, request: Request, db: Session = Depends(get_db)):
    user = require_auth_page(request, db)
    require_role(user, "super_admin")

    row = (
        db.query(StallUpdateRequest)
        .options(joinedload(StallUpdateRequest.stall).joinedload(Stall.category), joinedload(StallUpdateRequest.category))
        .filter(StallUpdateRequest.id == request_id)
        .first()
    )
    if not row:
        raise HTTPException(status_code=404, detail="Không tìm thấy yêu cầu")

    return {"item": serialize_update_request_detail(request, row)}


@app.post("/admin/update-requests/{request_id}/approve")
def approve_update(request_id: int, request: Request, db: Session = Depends(get_db)):
    user = require_auth_page(request, db)
    require_role(user, "super_admin")

    row = db.query(StallUpdateRequest).filter(StallUpdateRequest.id == request_id).first()
    if not row:
        raise HTTPException(status_code=404, detail="Không tìm thấy yêu cầu")
    if row.status == "approved":
        raise HTTPException(status_code=409, detail="Yêu cầu này đã được duyệt trước đó")
    if row.status == "rejected":
        raise HTTPException(status_code=409, detail="Yêu cầu này đã bị từ chối trước đó")

    stall = db.query(Stall).options(joinedload(Stall.translations)).filter(Stall.id == row.stall_id).first()
    if not stall:
        raise HTTPException(status_code=404, detail="Không tìm thấy gian hàng")

    stall.category_id = row.category_id
    stall.name = row.name
    stall.latitude = row.latitude
    stall.longitude = row.longitude
    stall.specialty_1 = row.specialty_1
    stall.specialty_2 = row.specialty_2
    stall.specialty_3 = row.specialty_3
    stall.poi_radius_m = row.poi_radius_m or 30
    stall.opening_hours = row.opening_hours
    stall.is_open = row.is_open
    stall.image_url = row.image_url
    stall.is_active = True
    stall.updated_at = datetime.utcnow()
    upsert_stall_translations(db, stall, row.name, row.script_vi)

    owner = db.query(User).filter(User.id == stall.created_by_user_id).first()
    if owner and not owner.is_active:
        owner.is_active = True
        owner.updated_at = datetime.utcnow()

    row.status = "approved"
    row.reviewed_at = datetime.utcnow()
    row.reviewed_by_user_id = user.id

    db.commit()
    return {"status": "success"}


@app.post("/admin/update-requests/{request_id}/reject")
def reject_update(
    request_id: int,
    request: Request,
    admin_note: str = Form(""),
    db: Session = Depends(get_db)
):
    user = require_auth_page(request, db)
    require_role(user, "super_admin")

    row = db.query(StallUpdateRequest).filter(StallUpdateRequest.id == request_id).first()
    if not row:
        raise HTTPException(status_code=404, detail="Không tìm thấy yêu cầu")
    if row.status == "approved":
        raise HTTPException(status_code=409, detail="Yêu cầu này đã được duyệt trước đó")
    if row.status == "rejected":
        raise HTTPException(status_code=409, detail="Yêu cầu này đã bị từ chối trước đó")

    stall = db.query(Stall).filter(Stall.id == row.stall_id).first()
    if stall and not stall.is_active:
        stall.is_deleted = True
        stall.updated_at = datetime.utcnow()

        owner = db.query(User).filter(User.id == stall.created_by_user_id).first()
        if owner and not owner.is_active:
            owner.is_active = True
            owner.updated_at = datetime.utcnow()

    row.status = "rejected"
    row.admin_note = admin_note
    row.reviewed_at = datetime.utcnow()
    row.reviewed_by_user_id = user.id
    db.commit()
    return {"status": "success"}


@app.post("/nearby")
def get_nearby(location: UserLocation, request: Request, db: Session = Depends(get_db)):
    stalls = (
        db.query(Stall)
        .options(joinedload(Stall.category), joinedload(Stall.translations).joinedload(StallTranslation.language))
        .filter(Stall.is_active == True, Stall.is_deleted == False)
        .all()
    )

    results = []
    for stall in stalls:
        dist = geodesic((location.lat, location.lng), (stall.latitude, stall.longitude)).kilometers
        results.append(serialize_stall_card(stall, request, dist))

    return sorted(results, key=lambda item: item["Distance"])


@app.get("/sync/version")
def get_sync_version(db: Session = Depends(get_db)):
    return {"version": get_content_sync_version(db)}


@app.post("/search")
def search_stalls(payload: SearchRequest, request: Request, db: Session = Depends(get_db)):
    query_terms = split_search_terms(payload.query)
    if not query_terms:
        return []

    stalls = (
        db.query(Stall)
        .options(joinedload(Stall.category), joinedload(Stall.translations).joinedload(StallTranslation.language))
        .filter(Stall.is_active == True, Stall.is_deleted == False)
        .all()
    )

    user_point = None
    if payload.lat is not None and payload.lng is not None:
        user_point = (payload.lat, payload.lng)

    ranked = []
    for stall in stalls:
        score = compute_search_score(stall, query_terms)
        if score <= 0:
            continue

        distance_km = geodesic(user_point, (stall.latitude, stall.longitude)).kilometers if user_point else 9999
        ranked.append((score, distance_km, serialize_stall_card(stall, request, distance_km)))

    ranked.sort(key=lambda item: (-item[0], item[1], item[2]["Name"]))
    limit = min(max(payload.limit, 1), 50)
    return [item[2] for item in ranked[:limit]]


@app.get("/stalls/map")
def get_map_stalls(
    request: Request,
    lat: Optional[float] = None,
    lng: Optional[float] = None,
    db: Session = Depends(get_db)
):
    stalls = (
        db.query(Stall)
        .options(joinedload(Stall.category), joinedload(Stall.translations).joinedload(StallTranslation.language))
        .filter(Stall.is_active == True, Stall.is_deleted == False)
        .all()
    )

    user_point = (lat, lng) if lat is not None and lng is not None else None
    results = []
    for stall in stalls:
        distance_km = geodesic(user_point, (stall.latitude, stall.longitude)).kilometers if user_point else 0
        results.append(serialize_stall_card(stall, request, distance_km))

    return sorted(results, key=lambda item: (item["Distance"], item["Name"]))


@app.get("/stalls/{stall_id}")
def get_stall_detail(
    stall_id: int,
    request: Request,
    lat: Optional[float] = None,
    lng: Optional[float] = None,
    db: Session = Depends(get_db)
):
    stall = (
        db.query(Stall)
        .options(joinedload(Stall.category), joinedload(Stall.translations).joinedload(StallTranslation.language))
        .filter(Stall.id == stall_id, Stall.is_active == True, Stall.is_deleted == False)
        .first()
    )
    if not stall:
        raise HTTPException(status_code=404, detail="Không tìm thấy gian hàng")

    distance_km = 0
    if lat is not None and lng is not None:
        distance_km = geodesic((lat, lng), (stall.latitude, stall.longitude)).kilometers

    return serialize_stall_card(stall, request, distance_km)


@app.post("/stalls/{stall_id}/reviews")
def submit_stall_review(
    stall_id: int,
    payload: RatingRequest,
    request: Request,
    db: Session = Depends(get_db)
):
    if payload.rating < 1 or payload.rating > 5:
        raise HTTPException(status_code=400, detail="Điểm đánh giá phải từ 1 đến 5 sao")

    stall = (
        db.query(Stall)
        .options(joinedload(Stall.category), joinedload(Stall.translations).joinedload(StallTranslation.language))
        .filter(Stall.id == stall_id, Stall.is_active == True, Stall.is_deleted == False)
        .first()
    )
    if not stall:
        raise HTTPException(status_code=404, detail="Không tìm thấy gian hàng")

    request_ip = get_request_ip(request)
    if request_ip:
        existing_review = (
            db.query(Review)
            .filter(
                Review.stall_id == stall.id,
                Review.ip_address == request_ip,
                Review.is_deleted == False
            )
            .first()
        )
        if existing_review:
            raise HTTPException(status_code=409, detail="Mỗi địa chỉ IP chỉ có thể đánh giá gian hàng này một lần")

    review = Review(
        stall_id=stall.id,
        rating=payload.rating,
        ip_address=request_ip or None,
        is_approved=True,
        created_at=datetime.utcnow(),
        updated_at=datetime.utcnow(),
        is_deleted=False
    )
    db.add(review)
    db.flush()
    refresh_stall_rating_summary(db, stall)
    db.commit()
    db.refresh(stall)

    distance_km = 0
    if payload.lat is not None and payload.lng is not None:
        distance_km = geodesic((payload.lat, payload.lng), (stall.latitude, stall.longitude)).kilometers

    return serialize_stall_card(stall, request, distance_km)


@app.get("/qr/resolve")
def resolve_qr(
    code: str,
    request: Request,
    lat: Optional[float] = None,
    lng: Optional[float] = None,
    session_id: str = "",
    device_id: str = "",
    source: str = "qr",
    db: Session = Depends(get_db)
):
    stall_id = resolve_stall_qr_code(code)
    stall = (
        db.query(Stall)
        .options(joinedload(Stall.category), joinedload(Stall.translations).joinedload(StallTranslation.language))
        .filter(Stall.id == stall_id, Stall.is_active == True, Stall.is_deleted == False)
        .first()
    )
    if not stall:
        raise HTTPException(status_code=404, detail="Không tìm thấy nội dung QR")

    distance_km = 0
    if lat is not None and lng is not None:
        distance_km = geodesic((lat, lng), (stall.latitude, stall.longitude)).kilometers

    ip_address = get_request_ip(request)
    is_duplicate_scan = has_recent_activity(
        db,
        QrScanLog,
        QrScanLog.scanned_at,
        stall.id,
        QR_DEDUP_WINDOW_SECONDS,
        session_id=session_id,
        device_id=device_id,
        ip_address=ip_address
    )
    if not is_duplicate_scan:
        db.add(QrScanLog(
            stall_id=stall.id,
            session_id=(session_id or "").strip(),
            device_id=(device_id or "").strip(),
            ip_address=ip_address,
            source=(source or "qr").strip() or "qr",
            latitude=lat,
            longitude=lng,
            scanned_at=datetime.utcnow()
        ))
        db.commit()

    return serialize_stall_card(stall, request, distance_km)


@app.post("/logs/listening")
def create_listening_log(
    request: Request,
    stall_id: int = Form(...),
    language_code: str = Form(...),
    session_id: str = Form(""),
    device_id: str = Form(""),
    duration_seconds: int = Form(0),
    lat: Optional[float] = Form(None),
    lng: Optional[float] = Form(None),
    source: str = Form("app"),
    db: Session = Depends(get_db)
):
    language = db.query(Language).filter(Language.code == language_code).first()
    stall = db.query(Stall).filter(Stall.id == stall_id, Stall.is_deleted == False, Stall.is_active == True).first()
    if not language or not stall:
        raise HTTPException(status_code=404, detail="Không tìm thấy dữ liệu")

    ip_address = get_request_ip(request)
    is_duplicate_listen = has_recent_activity(
        db,
        ListeningLog,
        ListeningLog.listened_at,
        stall_id,
        LISTEN_DEDUP_WINDOW_SECONDS,
        session_id=session_id,
        device_id=device_id,
        ip_address=ip_address
    )
    if is_duplicate_listen:
        return {"status": "deduplicated", "counted": False}

    db.add(ListeningLog(
        stall_id=stall_id,
        language_id=language.id,
        session_id=session_id,
        device_id=device_id,
        ip_address=ip_address,
        duration_seconds=duration_seconds,
        latitude=lat,
        longitude=lng,
        source=source,
        listened_at=datetime.utcnow()
    ))
    db.commit()
    return {"status": "success", "counted": True}


@app.post("/logs/location")
def create_location_log(
    request: Request,
    lat: float = Form(...),
    lng: float = Form(...),
    session_id: str = Form(""),
    device_id: str = Form(""),
    source: str = Form("app"),
    recorded_at: str = Form(""),
    db: Session = Depends(get_db)
):
    timestamp = datetime.utcnow()
    if recorded_at:
        try:
            timestamp = datetime.fromisoformat(recorded_at.replace("Z", "+00:00")).replace(tzinfo=None)
        except ValueError:
            timestamp = datetime.utcnow()

    normalized_session_id = (session_id or "").strip()
    normalized_device_id = (device_id or "").strip()
    if not normalized_session_id and not normalized_device_id:
        request_ip = get_request_ip(request)
        if request_ip:
            normalized_session_id = f"ip:{request_ip}"

    db.add(LocationLog(
        session_id=normalized_session_id,
        device_id=normalized_device_id,
        latitude=lat,
        longitude=lng,
        source=source,
        recorded_at=timestamp
    ))
    db.commit()
    return {"status": "success"}


@app.get("/audio/stalls/{stall_id}")
def get_stall_audio(
    stall_id: int,
    language_code: str,
    request: Request,
    db: Session = Depends(get_db)
):
    if not language_code:
        raise HTTPException(status_code=400, detail="Thiếu ngôn ngữ audio")

    stall = (
        db.query(Stall)
        .options(joinedload(Stall.translations).joinedload(StallTranslation.language))
        .filter(Stall.id == stall_id, Stall.is_deleted == False, Stall.is_active == True)
        .first()
    )
    if not stall:
        raise HTTPException(status_code=404, detail="Không tìm thấy gian hàng")

    language = db.query(Language).filter(Language.code == language_code, Language.is_active == True).first()
    if not language:
        raise HTTPException(status_code=404, detail="Không tìm thấy ngôn ngữ")

    script_text = translations_to_dict(stall).get(language_code)
    if not script_text and "-" in language_code:
        script_text = translations_to_dict(stall).get(language_code.split("-", 1)[0])
    if not script_text:
        raise HTTPException(status_code=404, detail="Không có script cho ngôn ngữ này")

    script_hash = get_script_hash(script_text, language_code)
    asset = (
        db.query(StallAudioAsset)
        .filter(StallAudioAsset.stall_id == stall.id, StallAudioAsset.language_id == language.id)
        .first()
    )

    if not asset or asset.script_hash != script_hash:
        try:
            audio_bytes = generate_audio_bytes(script_text, language_code)
        except Exception as ex:
            raise HTTPException(status_code=503, detail=f"Không thể tạo audio lúc này: {ex}")

        if asset:
            asset.script_hash = script_hash
            asset.audio_data = audio_bytes
            asset.mime_type = "audio/mpeg"
            asset.updated_at = datetime.utcnow()
        else:
            asset = StallAudioAsset(
                stall_id=stall.id,
                language_id=language.id,
                script_hash=script_hash,
                audio_data=audio_bytes,
                mime_type="audio/mpeg"
            )
            db.add(asset)
        db.commit()
        db.refresh(asset)

    return Response(
        content=asset.audio_data,
        media_type=asset.mime_type,
        headers={
            "Content-Disposition": f'inline; filename="stall-{stall.id}-{language_code}.mp3"',
            "Cache-Control": "public, max-age=86400",
            "X-Audio-Profile-Version": AUDIO_PROFILE_VERSION
        }
    )



