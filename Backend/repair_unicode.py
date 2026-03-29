from datetime import datetime
import os
from sqlalchemy import create_engine, text

DATABASE_URL = os.getenv("DATABASE_URL", "postgresql://admin:password123@db:5432/food_street_db")
engine = create_engine(DATABASE_URL)

owner_specs = {
    "chuoc": {
        "full_name": "Chủ quán Ốc Vĩnh Khánh",
        "stall_name": "Ốc Vĩnh Khánh",
        "script_vi": "Chào mừng bạn đến với Ốc Vĩnh Khánh, điểm hẹn hải sản đường phố nổi tiếng tại Quận 4.",
        "pending_name": "Ốc Vĩnh Khánh Chi Nhánh Tối",
        "pending_script": "Quán vừa bổ sung menu hải sản nướng và cập nhật lại khung giờ phục vụ buổi tối.",
    },
    "chubanhtrang": {
        "full_name": "Chủ quán Bánh Tráng Nướng",
        "stall_name": "Bánh Tráng Nướng Cô Út",
        "script_vi": "Bánh tráng nướng giòn thơm với nhiều loại topping, rất phù hợp cho buổi tối đi dạo.",
    },
    "chupho": {
        "full_name": "Chủ quán Phở Gà Chú Tư",
        "stall_name": "Phở Gà Chú Tư",
        "script_vi": "Một quán phở gà buổi sáng với nước dùng thanh, thịt gà mềm và khu vực ngồi gọn gàng.",
    },
    "chukem": {
        "full_name": "Chủ quán Kem Dừa",
        "stall_name": "Kem Dừa Cầu Đá",
        "script_vi": "Món kem dừa mát lạnh với topping dừa nạo và đậu phộng rang, phù hợp để tráng miệng.",
    },
}

with engine.begin() as conn:
    conn.execute(text("UPDATE users SET full_name = :name, updated_at = NOW() WHERE username = 'admin'"), {"name": "Quản trị hệ thống"})

    lang_id = conn.execute(text("SELECT id FROM languages WHERE code = 'vi' LIMIT 1")).scalar()

    for username, spec in owner_specs.items():
        conn.execute(
            text("UPDATE users SET full_name = :name, updated_at = NOW() WHERE username = :username"),
            {"name": spec["full_name"], "username": username},
        )
        user_id = conn.execute(text("SELECT id FROM users WHERE username = :username"), {"username": username}).scalar()
        if not user_id:
            continue

        stall_rows = conn.execute(
            text("SELECT id, name FROM stalls WHERE created_by_user_id = :user_id AND is_deleted = FALSE ORDER BY id ASC"),
            {"user_id": user_id},
        ).fetchall()
        if not stall_rows:
            continue

        keep_id = None
        for row in stall_rows:
            if row.name == spec["stall_name"]:
                keep_id = row.id
                break
        if keep_id is None:
            keep_id = stall_rows[0].id

        conn.execute(
            text("UPDATE stalls SET name = :name, updated_at = NOW() WHERE id = :stall_id"),
            {"name": spec["stall_name"], "stall_id": keep_id},
        )

        duplicate_ids = [row.id for row in stall_rows if row.id != keep_id]
        for duplicate_id in duplicate_ids:
            conn.execute(text("UPDATE listening_logs SET stall_id = :keep_id WHERE stall_id = :duplicate_id"), {"keep_id": keep_id, "duplicate_id": duplicate_id})
            conn.execute(text("UPDATE stall_update_requests SET stall_id = :keep_id WHERE stall_id = :duplicate_id"), {"keep_id": keep_id, "duplicate_id": duplicate_id})
            conn.execute(text("DELETE FROM stall_translations WHERE stall_id = :stall_id"), {"stall_id": duplicate_id})
            conn.execute(text("DELETE FROM stalls WHERE id = :stall_id"), {"stall_id": duplicate_id})

        if lang_id:
            exists = conn.execute(
                text("SELECT id FROM stall_translations WHERE stall_id = :stall_id AND language_id = :lang_id"),
                {"stall_id": keep_id, "lang_id": lang_id},
            ).scalar()
            if exists:
                conn.execute(
                    text("UPDATE stall_translations SET title = :title, script_text = :script_text, updated_at = NOW() WHERE id = :id"),
                    {"title": spec["stall_name"], "script_text": spec["script_vi"], "id": exists},
                )
            else:
                conn.execute(
                    text("INSERT INTO stall_translations (stall_id, language_id, title, description, script_text, is_auto_generated, translation_status, source_version, created_at, updated_at) VALUES (:stall_id, :lang_id, :title, NULL, :script_text, FALSE, 'approved', 1, NOW(), NOW())"),
                    {"stall_id": keep_id, "lang_id": lang_id, "title": spec["stall_name"], "script_text": spec["script_vi"]},
                )

        if username == "chuoc":
            conn.execute(
                text("UPDATE stall_update_requests SET stall_id = :stall_id, name = :name, script_vi = :script_vi WHERE submitted_by_user_id = :user_id AND status = 'pending'"),
                {"stall_id": keep_id, "name": spec["pending_name"], "script_vi": spec["pending_script"], "user_id": user_id},
            )

print("repair-ok")
