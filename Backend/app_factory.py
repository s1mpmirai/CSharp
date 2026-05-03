"""Application factory that wires middleware, static files, routers, and bootstrap steps."""

from __future__ import annotations

import os

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from fastapi.staticfiles import StaticFiles

from Backend.config import CORS_ORIGINS, UPLOAD_DIR
from Backend.db import Base, engine, ensure_schema_columns
from Backend.routers.admin import router as admin_router
from Backend.routers.owner import router as owner_router
from Backend.routers.public import router as public_router
from Backend.routers.web import router as web_router
from Backend.services import (
    ensure_default_web_users,
    ensure_minimum_stall_script_length,
    ensure_seed_data,
    ensure_stall_translation_coverage,
    normalize_reference_data,
    repair_legacy_review_aggregates,
    repair_reference_data_clean,
)


# Khởi tạo ứng dụng FastAPI, mount static, gắn router và chạy bootstrap dữ liệu.
def create_app() -> FastAPI:
    Base.metadata.create_all(bind=engine)
    ensure_schema_columns()
    os.makedirs(UPLOAD_DIR, exist_ok=True)
    repair_legacy_review_aggregates()
    ensure_seed_data()
    ensure_default_web_users()
    ensure_stall_translation_coverage()
    ensure_minimum_stall_script_length()
    normalize_reference_data()
    repair_reference_data_clean()

    app = FastAPI()
    app.add_middleware(
        CORSMiddleware,
        allow_origins=CORS_ORIGINS or ["*"],
        allow_methods=["*"],
        allow_headers=["*"],
        allow_credentials=True,
    )
    app.mount("/uploads", StaticFiles(directory=UPLOAD_DIR), name="uploads")
    app.include_router(web_router)
    app.include_router(owner_router)
    app.include_router(admin_router)
    app.include_router(public_router)
    return app
