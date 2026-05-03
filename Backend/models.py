"""ORM models grouped together so routes and services share one schema source."""

from __future__ import annotations

from datetime import datetime

from sqlalchemy import BigInteger, Boolean, Column, DateTime, Float, ForeignKey, Integer, LargeBinary, Numeric, String, Text
from sqlalchemy.orm import relationship

from .db import Base


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
