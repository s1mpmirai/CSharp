"""Database engine, session factory, and lightweight schema bootstrapping."""

from __future__ import annotations

from sqlalchemy import create_engine, text
from sqlalchemy.orm import Session, declarative_base, sessionmaker

from .config import DATABASE_URL


engine = create_engine(DATABASE_URL)
SessionLocal = sessionmaker(autocommit=False, autoflush=False, bind=engine)
Base = declarative_base()


# Cấp session database cho từng request và tự đóng sau khi dùng xong.
def get_db():
    db: Session = SessionLocal()
    try:
        yield db
    finally:
        db.close()


# Bổ sung các cột còn thiếu cho database cũ để hệ thống vẫn khởi động được.
def ensure_schema_columns() -> None:
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
