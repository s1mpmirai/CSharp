"""Owner-facing routes for stall management, requests, and notifications."""

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
from Backend.models import AdminNotificationRecipient, ListeningLog, QrScanLog, Review, Stall, StallUpdateRequest
from Backend.services import *

router = APIRouter()

@router.get("/owner/stall")
# Trả về thông tin gian hàng hiện tại của chủ quán.
def owner_stall(request: Request, db: Session = Depends(get_db)):
    user = require_auth_page(request, db)
    require_role(user, "stall_owner")
    stall = get_owner_stall(db, user.id)
    return {"stall": serialize_owner_stall_for_request(request, stall) if stall else None}


@router.get("/owner/dashboard")
# Tổng hợp số liệu dashboard, xu hướng nghe và trạng thái yêu cầu của chủ quán.
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


@router.get("/owner/update-requests")
# Liệt kê các yêu cầu cập nhật gần đây của chủ quán.
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


@router.get("/owner/notifications")
# Gom thông báo từ admin và kết quả duyệt yêu cầu cho chủ quán.
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


@router.post("/owner/update-requests/{request_id}/read")
# Đánh dấu một thông báo yêu cầu cập nhật là đã đọc.
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


@router.post("/owner/admin-notifications/{recipient_id}/read")
# Đánh dấu một thông báo admin là đã đọc.
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


@router.delete("/owner/update-requests/{request_id}")
# Ẩn một thông báo yêu cầu cập nhật khỏi hộp thư chủ quán.
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


@router.delete("/owner/admin-notifications/{recipient_id}")
# Ẩn một thông báo admin khỏi hộp thư chủ quán.
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


@router.delete("/owner/update-requests")
# Ẩn toàn bộ thông báo yêu cầu cập nhật của chủ quán.
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


@router.delete("/owner/admin-notifications")
# Ẩn toàn bộ thông báo admin của chủ quán.
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


@router.post("/owner/stall")
# Tạo mới gian hàng đầu tiên của chủ quán và gửi yêu cầu duyệt.
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


@router.post("/owner/stall-update-request")
# Tạo hoặc cập nhật yêu cầu chỉnh sửa gian hàng đang hoạt động.
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
