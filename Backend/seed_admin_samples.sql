BEGIN;

INSERT INTO stalls (
    category_id,
    name,
    latitude,
    longitude,
    image_url,
    opening_hours,
    is_open,
    is_active,
    rating_avg,
    reviews_count,
    created_by_user_id,
    created_at,
    updated_at,
    is_deleted
)
SELECT 1, 'Ốc Vĩnh Khánh', 10.759850, 106.704750, NULL, '17:00-23:30', TRUE, TRUE, 4.8, 120,
       u.id, NOW(), NOW(), FALSE
FROM users u
WHERE u.username = 'chuoc'
  AND NOT EXISTS (SELECT 1 FROM stalls WHERE name = 'Ốc Vĩnh Khánh');

INSERT INTO stalls (
    category_id,
    name,
    latitude,
    longitude,
    image_url,
    opening_hours,
    is_open,
    is_active,
    rating_avg,
    reviews_count,
    created_by_user_id,
    created_at,
    updated_at,
    is_deleted
)
SELECT 4, 'Bánh Tráng Nướng Cô Út', 10.762150, 106.701920, NULL, '15:00-22:00', TRUE, TRUE, 4.6, 85,
       u.id, NOW(), NOW(), FALSE
FROM users u
WHERE u.username = 'chubanhtrang'
  AND NOT EXISTS (SELECT 1 FROM stalls WHERE name = 'Bánh Tráng Nướng Cô Út');

INSERT INTO stalls (
    category_id,
    name,
    latitude,
    longitude,
    image_url,
    opening_hours,
    is_open,
    is_active,
    rating_avg,
    reviews_count,
    created_by_user_id,
    created_at,
    updated_at,
    is_deleted
)
SELECT 3, 'Phở Gà Chú Tư', 10.764120, 106.698880, NULL, '06:00-13:30', TRUE, TRUE, 4.7, 64,
       u.id, NOW(), NOW(), FALSE
FROM users u
WHERE u.username = 'chupho'
  AND NOT EXISTS (SELECT 1 FROM stalls WHERE name = 'Phở Gà Chú Tư');

INSERT INTO stalls (
    category_id,
    name,
    latitude,
    longitude,
    image_url,
    opening_hours,
    is_open,
    is_active,
    rating_avg,
    reviews_count,
    created_by_user_id,
    created_at,
    updated_at,
    is_deleted
)
SELECT 5, 'Kem Dừa Cầu Đá', 10.761330, 106.706210, NULL, '14:00-22:30', TRUE, TRUE, 4.5, 41,
       u.id, NOW(), NOW(), FALSE
FROM users u
WHERE u.username = 'chukem'
  AND NOT EXISTS (SELECT 1 FROM stalls WHERE name = 'Kem Dừa Cầu Đá');

UPDATE stalls SET created_by_user_id = (SELECT id FROM users WHERE username = 'chuoc'), updated_at = NOW()
WHERE name = 'Ốc Vĩnh Khánh' AND (created_by_user_id IS NULL OR created_by_user_id <> (SELECT id FROM users WHERE username = 'chuoc'));

UPDATE stalls SET created_by_user_id = (SELECT id FROM users WHERE username = 'chubanhtrang'), updated_at = NOW()
WHERE name = 'Bánh Tráng Nướng Cô Út' AND (created_by_user_id IS NULL OR created_by_user_id <> (SELECT id FROM users WHERE username = 'chubanhtrang'));

UPDATE stalls SET created_by_user_id = (SELECT id FROM users WHERE username = 'chupho'), updated_at = NOW()
WHERE name = 'Phở Gà Chú Tư' AND (created_by_user_id IS NULL OR created_by_user_id <> (SELECT id FROM users WHERE username = 'chupho'));

UPDATE stalls SET created_by_user_id = (SELECT id FROM users WHERE username = 'chukem'), updated_at = NOW()
WHERE name = 'Kem Dừa Cầu Đá' AND (created_by_user_id IS NULL OR created_by_user_id <> (SELECT id FROM users WHERE username = 'chukem'));

INSERT INTO stall_translations (
    stall_id,
    language_id,
    title,
    description,
    script_text,
    is_auto_generated,
    translation_status,
    source_version,
    created_at,
    updated_at
)
SELECT s.id, l.id, 'Ốc Vĩnh Khánh', NULL,
       'Chào mừng bạn đến với Ốc Vĩnh Khánh, điểm hẹn hải sản đường phố nổi tiếng tại Quận 4.',
       FALSE, 'approved', 1, NOW(), NOW()
FROM stalls s
JOIN languages l ON l.code = 'vi'
WHERE s.name = 'Ốc Vĩnh Khánh'
ON CONFLICT (stall_id, language_id) DO NOTHING;

INSERT INTO stall_translations (
    stall_id,
    language_id,
    title,
    description,
    script_text,
    is_auto_generated,
    translation_status,
    source_version,
    created_at,
    updated_at
)
SELECT s.id, l.id, 'Bánh Tráng Nướng Cô Út', NULL,
       'Bánh tráng nướng giòn thơm với nhiều loại topping, rất phù hợp cho buổi tối đi dạo.',
       FALSE, 'approved', 1, NOW(), NOW()
FROM stalls s
JOIN languages l ON l.code = 'vi'
WHERE s.name = 'Bánh Tráng Nướng Cô Út'
ON CONFLICT (stall_id, language_id) DO NOTHING;

INSERT INTO stall_translations (
    stall_id,
    language_id,
    title,
    description,
    script_text,
    is_auto_generated,
    translation_status,
    source_version,
    created_at,
    updated_at
)
SELECT s.id, l.id, 'Phở Gà Chú Tư', NULL,
       'Một quán phở gà buổi sáng với nước dùng thanh, thịt gà mềm và khu vực ngồi gọn gàng.',
       FALSE, 'approved', 1, NOW(), NOW()
FROM stalls s
JOIN languages l ON l.code = 'vi'
WHERE s.name = 'Phở Gà Chú Tư'
ON CONFLICT (stall_id, language_id) DO NOTHING;

INSERT INTO stall_translations (
    stall_id,
    language_id,
    title,
    description,
    script_text,
    is_auto_generated,
    translation_status,
    source_version,
    created_at,
    updated_at
)
SELECT s.id, l.id, 'Kem Dừa Cầu Đá', NULL,
       'Món kem dừa mát lạnh với topping dừa nạo và đậu phộng rang, phù hợp để tráng miệng.',
       FALSE, 'approved', 1, NOW(), NOW()
FROM stalls s
JOIN languages l ON l.code = 'vi'
WHERE s.name = 'Kem Dừa Cầu Đá'
ON CONFLICT (stall_id, language_id) DO NOTHING;

INSERT INTO listening_logs (
    stall_id,
    language_id,
    session_id,
    device_id,
    duration_seconds,
    source,
    listened_at
)
SELECT s.id, l.id, 'seed-oc-1', 'demo-device-1', 42, 'app', NOW() - INTERVAL '2 day'
FROM stalls s
JOIN languages l ON l.code = 'vi'
WHERE s.name = 'Ốc Vĩnh Khánh'
  AND NOT EXISTS (SELECT 1 FROM listening_logs WHERE session_id = 'seed-oc-1');

INSERT INTO listening_logs (
    stall_id,
    language_id,
    session_id,
    device_id,
    duration_seconds,
    source,
    listened_at
)
SELECT s.id, l.id, 'seed-oc-2', 'demo-device-2', 55, 'app', NOW() - INTERVAL '1 day'
FROM stalls s
JOIN languages l ON l.code = 'en'
WHERE s.name = 'Ốc Vĩnh Khánh'
  AND NOT EXISTS (SELECT 1 FROM listening_logs WHERE session_id = 'seed-oc-2');

INSERT INTO listening_logs (
    stall_id,
    language_id,
    session_id,
    device_id,
    duration_seconds,
    source,
    listened_at
)
SELECT s.id, l.id, 'seed-bt-1', 'demo-device-3', 36, 'app', NOW() - INTERVAL '12 hour'
FROM stalls s
JOIN languages l ON l.code = 'vi'
WHERE s.name = 'Bánh Tráng Nướng Cô Út'
  AND NOT EXISTS (SELECT 1 FROM listening_logs WHERE session_id = 'seed-bt-1');

INSERT INTO listening_logs (
    stall_id,
    language_id,
    session_id,
    device_id,
    duration_seconds,
    source,
    listened_at
)
SELECT s.id, l.id, 'seed-pho-1', 'demo-device-4', 28, 'app', NOW() - INTERVAL '6 hour'
FROM stalls s
JOIN languages l ON l.code = 'vi'
WHERE s.name = 'Phở Gà Chú Tư'
  AND NOT EXISTS (SELECT 1 FROM listening_logs WHERE session_id = 'seed-pho-1');

INSERT INTO stall_update_requests (
    stall_id,
    submitted_by_user_id,
    category_id,
    name,
    latitude,
    longitude,
    opening_hours,
    is_open,
    script_vi,
    image_url,
    status,
    submitted_at
)
SELECT s.id,
       u.id,
       s.category_id,
       'Ốc Vĩnh Khánh Chi Nhánh Tối',
       s.latitude,
       s.longitude,
       '16:30-23:45',
       TRUE,
       'Quán vừa bổ sung menu hải sản nướng và cập nhật lại khung giờ phục vụ buổi tối.',
       s.image_url,
       'pending',
       NOW() - INTERVAL '3 hour'
FROM stalls s
JOIN users u ON u.username = 'chuoc'
WHERE s.name = 'Ốc Vĩnh Khánh'
  AND NOT EXISTS (
      SELECT 1
      FROM stall_update_requests r
      WHERE r.stall_id = s.id AND r.status = 'pending'
  );

COMMIT;
