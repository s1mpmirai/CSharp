"""Public and app-consumed API routes for discovery, search, logging, QR, and audio."""

from __future__ import annotations

from fastapi import APIRouter, Depends, Form, HTTPException, Request, Response
from sqlalchemy.orm import Session, joinedload
from datetime import datetime
from geopy.distance import geodesic

from Backend.config import AUDIO_PROFILE_VERSION, LISTEN_DEDUP_WINDOW_SECONDS, QR_DEDUP_WINDOW_SECONDS
from Backend.db import get_db
from Backend.models import Language, ListeningLog, LocationLog, QrScanLog, Review, Stall, StallAudioAsset, StallTranslation
from Backend.schemas import RatingRequest, SearchRequest, UserLocation
from Backend.services import *

router = APIRouter()

@router.post("/nearby")
# Trả về danh sách gian hàng đang hoạt động theo khoảng cách từ vị trí người dùng.
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


@router.get("/sync/version")
# Trả về mã phiên bản đồng bộ nội dung cho app client.
def get_sync_version(db: Session = Depends(get_db)):
    return {"version": get_content_sync_version(db)}


@router.post("/search")
# Tìm kiếm gian hàng theo từ khóa và ưu tiên theo điểm phù hợp.
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


@router.get("/stalls/map")
# Trả về dữ liệu gian hàng để hiển thị trên bản đồ.
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


@router.get("/stalls/{stall_id}")
# Lấy chi tiết một gian hàng và khoảng cách tới người dùng nếu có.
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


@router.post("/stalls/{stall_id}/reviews")
# Ghi nhận đánh giá sao cho gian hàng và cập nhật điểm trung bình.
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


@router.get("/qr/resolve")
# Giải mã QR gian hàng, ghi log quét và trả về dữ liệu gian hàng.
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


@router.post("/logs/listening")
# Ghi nhận lượt nghe audio nếu không trùng lặp trong thời gian ngắn.
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


@router.post("/logs/location")
# Ghi nhận vị trí thiết bị để phục vụ thống kê thời gian thực.
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


@router.get("/audio/stalls/{stall_id}")
# Sinh hoặc trả lại audio cache cho script giới thiệu của gian hàng.
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
