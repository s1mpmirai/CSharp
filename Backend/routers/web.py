"""Routes for HTML pages, session endpoints, and lightweight shared resources."""

from __future__ import annotations

from fastapi import APIRouter, Depends, HTTPException, Request
from fastapi.responses import JSONResponse, RedirectResponse
from sqlalchemy.orm import Session, joinedload
from datetime import datetime

from Backend.config import COOKIE_SECURE, SESSION_COOKIE, TOKEN_HOURS
from Backend.db import get_db
from Backend.models import Category, User
from Backend.schemas import LoginRequest
from Backend.services import *

router = APIRouter()

@router.get("/thumbnails/{filename}")
# Tạo thumbnail ảnh upload để giao diện tải nhanh hơn.
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


@router.get("/")
# Điều hướng người dùng đến trang phù hợp theo trạng thái đăng nhập và vai trò.
def root(request: Request, db: Session = Depends(get_db)):
    user = get_current_user_from_request(request, db)
    if not user:
        return RedirectResponse(url="/login", status_code=302)
    if user.role.name == "super_admin":
        return RedirectResponse(url="/superadmin", status_code=302)
    return RedirectResponse(url="/owner", status_code=302)


@router.get("/login")
# Trả về trang đăng nhập hoặc chuyển hướng nếu người dùng đã đăng nhập.
def login_page(request: Request, db: Session = Depends(get_db)):
    user = get_current_user_from_request(request, db)
    if user:
        return RedirectResponse(url=get_home_redirect_for_user(user), status_code=302)
    return web_file_response("login.html")


@router.get("/owner")
# Mở giao diện chủ quán phù hợp với trạng thái gian hàng hiện tại.
def owner_page(request: Request, db: Session = Depends(get_db)):
    user = get_current_user_from_request(request, db)
    if not user:
        return RedirectResponse(url="/login", status_code=302)
    if not user.role or user.role.name != "stall_owner":
        return RedirectResponse(url=get_home_redirect_for_user(user), status_code=302)
    stall = get_owner_stall(db, user.id)
    page = "owner-dashboard.html" if stall else "admin.html"
    return web_file_response(page)


@router.get("/superadmin")
# Mở giao diện quản trị cấp cao cho tài khoản super admin.
def superadmin_page(request: Request, db: Session = Depends(get_db)):
    user = get_current_user_from_request(request, db)
    if not user:
        return RedirectResponse(url="/login", status_code=302)
    if not user.role or user.role.name != "super_admin":
        return RedirectResponse(url=get_home_redirect_for_user(user), status_code=302)
    return web_file_response("superadmin.html")


@router.post("/auth/login")
# Xử lý đăng nhập, kiểm tra tài khoản và thiết lập cookie phiên.
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


@router.post("/auth/logout")
# Đăng xuất bằng cách xóa cookie phiên hiện tại.
def logout():
    response = JSONResponse({"status": "success"})
    response.delete_cookie(SESSION_COOKIE, samesite="lax", secure=COOKIE_SECURE)
    return response


@router.get("/auth/me")
# Trả về thông tin phiên hiện tại và trạng thái gian hàng của owner.
def auth_me(request: Request, db: Session = Depends(get_db)):
    user = require_auth_page(request, db)
    owner_stall = get_owner_stall(db, user.id) if user.role.name == "stall_owner" else None
    return {
        "user": serialize_user(user),
        "has_stall": owner_stall is not None,
        "pending_initial_approval": owner_is_waiting_for_initial_approval(db, user.id) if user.role.name == "stall_owner" else False
    }


@router.get("/categories")
# Trả về danh sách danh mục đang hoạt động cho biểu mẫu và giao diện.
def categories(request: Request, db: Session = Depends(get_db)):
    items = db.query(Category).filter(Category.is_active == True).order_by(Category.id.asc()).all()
    return [{"id": item.id, "name": item.name} for item in items]
