-- Safe migration helpers for an existing FoodStreet database
-- No DROP/TRUNCATE and no bulk destructive deletes.

BEGIN;

INSERT INTO roles (name, description, created_at, updated_at)
VALUES
    ('super_admin', 'Qu?n tr? h? th?ng', NOW(), NOW()),
    ('stall_owner', 'Ch? gian hàng', NOW(), NOW())
ON CONFLICT (name) DO UPDATE
SET
    description = EXCLUDED.description,
    updated_at = NOW();

INSERT INTO languages (code, name, native_name, locale_code, sort_order, is_active, created_at, updated_at)
VALUES
    ('vi', 'Vietnamese', 'Ti?ng Vi?t', 'vi-VN', 1, TRUE, NOW(), NOW()),
    ('en', 'English', 'English', 'en-US', 2, TRUE, NOW(), NOW()),
    ('zh-CN', 'Chinese', '??', 'zh-CN', 3, TRUE, NOW(), NOW()),
    ('ja', 'Japanese', '???', 'ja-JP', 4, TRUE, NOW(), NOW()),
    ('ko', 'Korean', '???', 'ko-KR', 5, TRUE, NOW(), NOW())
ON CONFLICT (code) DO UPDATE
SET
    name = EXCLUDED.name,
    native_name = EXCLUDED.native_name,
    locale_code = EXCLUDED.locale_code,
    sort_order = EXCLUDED.sort_order,
    is_active = TRUE,
    updated_at = NOW();

INSERT INTO categories (slug, name, is_active, created_at, updated_at)
VALUES
    ('seafood', 'H?i s?n', TRUE, NOW(), NOW()),
    ('grilled', 'Ð? nu?ng', TRUE, NOW(), NOW()),
    ('noodles', 'Món nu?c', TRUE, NOW(), NOW()),
    ('snacks', 'An v?t', TRUE, NOW(), NOW()),
    ('desserts', 'Tráng mi?ng', TRUE, NOW(), NOW()),
    ('rice', 'Com', TRUE, NOW(), NOW()),
    ('dumplings', 'Há c?o', TRUE, NOW(), NOW()),
    ('specialties', 'Ð?c s?n', TRUE, NOW(), NOW())
ON CONFLICT (slug) DO UPDATE
SET
    name = EXCLUDED.name,
    is_active = TRUE,
    updated_at = NOW();

INSERT INTO users (role_id, username, password_hash, full_name, email, is_active, created_at, updated_at)
SELECT
    r.id,
    'admin',
    'pbkdf2_sha256$390000$SHGU1N51AvWzZqNDolhXeA$JKlMGBHNFqXIXeM2SJU08lbOJneu_JUwqq9tg-K_aRs',
    'Qu?n tr? h? th?ng',
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
    COALESCE(NULLIF(src.script_vi, ''), 'N?i dung thuy?t minh dang du?c c?p nh?t.'),
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

UPDATE users u
SET
    is_active = CASE
        WHEN EXISTS (
            SELECT 1
            FROM stalls s
            WHERE s.created_by_user_id = u.id
              AND s.is_deleted = FALSE
              AND s.is_active = TRUE
        ) THEN TRUE
        WHEN EXISTS (
            SELECT 1
            FROM stalls s
            JOIN stall_update_requests r ON r.stall_id = s.id
            WHERE s.created_by_user_id = u.id
              AND s.is_deleted = FALSE
              AND s.is_active = FALSE
              AND r.status = 'pending'
        ) THEN FALSE
        ELSE TRUE
    END,
    updated_at = NOW()
WHERE EXISTS (
    SELECT 1
    FROM roles r
    WHERE r.id = u.role_id
      AND r.name = 'stall_owner'
);

UPDATE stall_translations
SET updated_at = NOW()
WHERE updated_at IS NULL;

UPDATE stalls
SET updated_at = NOW()
WHERE updated_at IS NULL;

UPDATE categories
SET updated_at = NOW()
WHERE updated_at IS NULL;

UPDATE stall_update_requests
SET reviewed_at = COALESCE(reviewed_at, NOW())
WHERE status IN ('approved', 'rejected')
  AND reviewed_at IS NULL;

COMMIT;

