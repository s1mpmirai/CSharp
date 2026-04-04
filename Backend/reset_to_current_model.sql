BEGIN;

-- =========================================================
-- StreetFeast DB reset/cleanup for the current product flow
-- Target flow:
-- 1. Only `super_admin` is seeded by default.
-- 2. New stall owners are created from superadmin UI.
-- 3. First stall submission creates a pending approval request.
-- 4. Owners waiting for first approval must not have an active stall.
-- 5. App/web read scripts from `stall_translations`, not legacy seed helpers.
-- =========================================================

-- ---------------------------------------------------------
-- 1. Normalize roles
-- ---------------------------------------------------------
INSERT INTO roles (name, description, created_at, updated_at)
VALUES
    ('super_admin', 'Quản trị hệ thống', NOW(), NOW()),
    ('stall_owner', 'Chủ gian hàng', NOW(), NOW())
ON CONFLICT (name) DO UPDATE
SET
    description = EXCLUDED.description,
    updated_at = NOW();

-- ---------------------------------------------------------
-- 2. Normalize languages
-- ---------------------------------------------------------
INSERT INTO languages (code, name, native_name, locale_code, sort_order, is_active, created_at, updated_at)
VALUES
    ('vi', 'Vietnamese', 'Tiếng Việt', 'vi-VN', 1, TRUE, NOW(), NOW()),
    ('en', 'English', 'English', 'en-US', 2, TRUE, NOW(), NOW()),
    ('zh-CN', 'Chinese', '中文', 'zh-CN', 3, TRUE, NOW(), NOW()),
    ('ja', 'Japanese', '日本語', 'ja-JP', 4, TRUE, NOW(), NOW()),
    ('ko', 'Korean', '한국어', 'ko-KR', 5, TRUE, NOW(), NOW())
ON CONFLICT (code) DO UPDATE
SET
    name = EXCLUDED.name,
    native_name = EXCLUDED.native_name,
    locale_code = EXCLUDED.locale_code,
    sort_order = EXCLUDED.sort_order,
    is_active = TRUE,
    updated_at = NOW();

-- ---------------------------------------------------------
-- 3. Normalize categories used by app/web
-- ---------------------------------------------------------
INSERT INTO categories (slug, name, is_active, created_at, updated_at)
VALUES
    ('cat-1', 'Hải sản', TRUE, NOW(), NOW()),
    ('cat-2', 'Đồ nướng', TRUE, NOW(), NOW()),
    ('cat-3', 'Món nước', TRUE, NOW(), NOW()),
    ('cat-4', 'Ăn vặt', TRUE, NOW(), NOW()),
    ('cat-5', 'Tráng miệng', TRUE, NOW(), NOW())
ON CONFLICT (slug) DO UPDATE
SET
    name = EXCLUDED.name,
    is_active = TRUE,
    updated_at = NOW();

-- ---------------------------------------------------------
-- 4. Ensure default admin exists and is active
-- ---------------------------------------------------------
INSERT INTO users (role_id, username, password_hash, full_name, email, is_active, created_at, updated_at)
SELECT
    r.id,
    'admin',
    'pbkdf2_sha256$390000$SHGU1N51AvWzZqNDolhXeA$JKlMGBHNFqXIXeM2SJU08lbOJneu_JUwqq9tg-K_aRs',
    'Quản trị hệ thống',
    'admin@streetfeast.local',
    TRUE,
    NOW(),
    NOW()
FROM roles r
WHERE r.name = 'super_admin'
ON CONFLICT (username) DO UPDATE
SET
    role_id = EXCLUDED.role_id,
    full_name = EXCLUDED.full_name,
    email = EXCLUDED.email,
    is_active = TRUE,
    updated_at = NOW();

-- ---------------------------------------------------------
-- 5. Remove legacy seeded owners from old flow
--    These users used to be auto-created and auto-bound to stalls.
--    We keep the stalls, but detach ownership so the current owner
--    onboarding flow is not polluted by historical seed data.
-- ---------------------------------------------------------
WITH legacy_users AS (
    SELECT id, username
    FROM users
    WHERE username IN ('chuoc', 'chubanhtrang', 'chupho', 'chukem')
       OR username ~ '^owner[0-9]{3}(_[0-9]+)?$'
)
UPDATE stalls
SET
    created_by_user_id = NULL,
    updated_at = NOW()
WHERE created_by_user_id IN (SELECT id FROM legacy_users);

DELETE FROM stall_update_requests
WHERE submitted_by_user_id IN (
    SELECT id
    FROM users
    WHERE username IN ('chuoc', 'chubanhtrang', 'chupho', 'chukem')
       OR username ~ '^owner[0-9]{3}(_[0-9]+)?$'
);

DELETE FROM users
WHERE username IN ('chuoc', 'chubanhtrang', 'chupho', 'chukem')
   OR username ~ '^owner[0-9]{3}(_[0-9]+)?$';

-- ---------------------------------------------------------
-- 6. Repair owner accounts against current onboarding flow
--    - if owner has an active stall, account must be active
--    - if owner has no active stall but has a pending first request,
--      account should stay locked until reviewed
--    - otherwise owner can log in and create/resubmit
-- ---------------------------------------------------------
WITH owner_role AS (
    SELECT id FROM roles WHERE name = 'stall_owner'
),
owner_status AS (
    SELECT
        u.id AS user_id,
        EXISTS (
            SELECT 1
            FROM stalls s
            WHERE s.created_by_user_id = u.id
              AND s.is_deleted = FALSE
              AND s.is_active = TRUE
        ) AS has_active_stall,
        EXISTS (
            SELECT 1
            FROM stalls s
            JOIN stall_update_requests r ON r.stall_id = s.id
            WHERE s.created_by_user_id = u.id
              AND s.is_deleted = FALSE
              AND s.is_active = FALSE
              AND r.status = 'pending'
        ) AS waiting_first_approval
    FROM users u
    JOIN owner_role r ON u.role_id = r.id
)
UPDATE users u
SET
    is_active = CASE
        WHEN os.has_active_stall THEN TRUE
        WHEN os.waiting_first_approval THEN FALSE
        ELSE TRUE
    END,
    updated_at = NOW()
FROM owner_status os
WHERE u.id = os.user_id;

-- ---------------------------------------------------------
-- 7. Ensure active stalls have a Vietnamese translation row
--    App/audio/UI rely on translations rather than legacy script columns.
-- ---------------------------------------------------------
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
SELECT
    s.id,
    l.id,
    s.name,
    NULL,
    COALESCE(
        NULLIF(src.script_vi, ''),
        'Nội dung thuyết minh đang được cập nhật.'
    ),
    FALSE,
    'approved',
    1,
    NOW(),
    NOW()
FROM stalls s
CROSS JOIN languages l
LEFT JOIN stall_update_requests src
    ON src.stall_id = s.id
   AND src.status IN ('approved', 'pending')
LEFT JOIN stall_translations t
    ON t.stall_id = s.id
   AND t.language_id = l.id
WHERE l.code = 'vi'
  AND s.is_deleted = FALSE
  AND t.id IS NULL;

-- ---------------------------------------------------------
-- 8. Make sure all translation rows have clean version timestamps
--    so /sync/version changes are visible to the app.
-- ---------------------------------------------------------
UPDATE stall_translations
SET updated_at = NOW()
WHERE updated_at IS NULL;

UPDATE stalls
SET updated_at = NOW()
WHERE updated_at IS NULL;

UPDATE categories
SET updated_at = NOW()
WHERE updated_at IS NULL;

-- ---------------------------------------------------------
-- 9. When approved/rejected requests still have null review timestamps,
--    backfill them for cleaner admin/owner history.
-- ---------------------------------------------------------
UPDATE stall_update_requests
SET reviewed_at = COALESCE(reviewed_at, NOW())
WHERE status IN ('approved', 'rejected')
  AND reviewed_at IS NULL;

COMMIT;
