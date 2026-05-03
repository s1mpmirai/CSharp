"""Administrator routes for owner accounts, approvals, notifications, and analytics."""

from __future__ import annotations

from fastapi import APIRouter, Depends, File, Form, HTTPException, Request, UploadFile
from sqlalchemy import func
from sqlalchemy.orm import Session, joinedload
from datetime import datetime, timedelta
import os
import shutil
import uuid

from Backend.config import LISTEN_DEDUP_WINDOW_SECONDS, QR_DEDUP_WINDOW_SECONDS, UPLOAD_DIR
from Backend.db import get_db
from Backend.models import AdminNotification, AdminNotificationRecipient, ListeningLog, LocationLog, QrScanLog, Role, Stall, StallUpdateRequest, User
from Backend.schemas import CreateAdminNotificationRequest, CreateOwnerRequest, UpdateUserRequest
from Backend.services import *

router = APIRouter()

@router.get("/admin/dashboard")
# Tổng hợp số liệu quản trị, heatmap và danh sách người dùng đang hoạt động gần đây.
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
            "total_listens": total_listens,
            "pending_updates": pending_updates,
            "average_listen_seconds": round(float(average_listen_seconds or 0), 1),
            "unique_sessions": int(unique_sessions or 0),
            "unique_devices": int(unique_devices or 0),
            "active_users_5m": len(active_user_keys)
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


@router.get("/admin/users")
# Liệt kê toàn bộ tài khoản chủ quán cho màn hình quản trị.
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


@router.get("/admin/users/{user_id}")
# Lấy chi tiết một tài khoản chủ quán và gian hàng liên quan.
def admin_user_detail(user_id: int, request: Request, db: Session = Depends(get_db)):
    user = require_auth_page(request, db)
    require_role(user, "super_admin")

    target_user = db.query(User).options(joinedload(User.role)).filter(User.id == user_id).first()
    if not target_user:
        raise HTTPException(status_code=404, detail="Không tìm thấy người dùng")

    stall = get_owner_stall(db, target_user.id) if target_user.role and target_user.role.name == "stall_owner" else None
    return {"user": serialize_admin_user(request, target_user, stall)}


@router.post("/admin/users")
# Tạo mới tài khoản chủ quán từ dashboard quản trị.
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


@router.get("/admin/notification-recipients")
# Liệt kê nhóm owner có thể nhận thông báo từ admin.
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


@router.post("/admin/notifications")
# Tạo và gửi một thông báo admin tới nhóm người nhận đã chọn.
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


@router.get("/admin/notifications")
# Trả về lịch sử các thông báo admin đã gửi cùng trạng thái đọc.
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


@router.put("/admin/users/{user_id}")
# Cập nhật thông tin đăng nhập và hồ sơ của một người dùng.
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


@router.post("/admin/users/{user_id}/manage")
# Quản lý đồng thời thông tin tài khoản owner và dữ liệu gian hàng của họ.
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


@router.patch("/admin/users/{user_id}/hide")
# Ẩn hoặc khóa một chủ gian hàng và vô hiệu hóa gian hàng liên quan.
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


@router.patch("/admin/users/{user_id}/activate")
# Kích hoạt lại một chủ gian hàng và gian hàng liên quan.
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


@router.get("/admin/update-requests")
# Liệt kê các yêu cầu cập nhật gian hàng đang chờ admin duyệt.
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


@router.get("/admin/update-requests/{request_id}")
# Lấy chi tiết một yêu cầu cập nhật để admin so sánh và xử lý.
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


@router.post("/admin/update-requests/{request_id}/approve")
# Duyệt yêu cầu cập nhật và áp dụng dữ liệu mới vào gian hàng.
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


@router.post("/admin/update-requests/{request_id}/reject")
# Từ chối yêu cầu cập nhật và ghi nhận ghi chú phản hồi của admin.
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
