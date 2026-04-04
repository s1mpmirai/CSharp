BEGIN;

WITH owner_role AS (
    SELECT id
    FROM roles
    WHERE name = 'stall_owner'
    LIMIT 1
),
missing_stalls AS (
    SELECT
        s.id AS stall_id,
        s.name AS stall_name,
        CONCAT('owner_stall_', s.id) AS username,
        CONCAT('owner-stall-', s.id, '@streetfeast.local') AS email,
        CONCAT('Chủ gian hàng ', s.name) AS full_name
    FROM stalls s
    WHERE s.is_deleted = FALSE
      AND s.created_by_user_id IS NULL
),
inserted_users AS (
    INSERT INTO users (
        role_id,
        username,
        password_hash,
        full_name,
        email,
        is_active,
        created_at,
        updated_at
    )
    SELECT
        owner_role.id,
        missing_stalls.username,
        'pbkdf2_sha256$390000$b7bb25e9a4389b665248877a36d78e0f$fy6ZQXsY9uKklUZHWJfw7tBVSqH2ZOKfS6NEjZ222M0',
        missing_stalls.full_name,
        missing_stalls.email,
        TRUE,
        NOW(),
        NOW()
    FROM missing_stalls
    CROSS JOIN owner_role
    WHERE NOT EXISTS (
        SELECT 1
        FROM users u
        WHERE u.username = missing_stalls.username
    )
    RETURNING id, username
)
UPDATE users u
SET
    full_name = missing_stalls.full_name,
    email = missing_stalls.email,
    is_active = TRUE,
    updated_at = NOW()
FROM missing_stalls
WHERE u.username = missing_stalls.username;

WITH missing_stalls AS (
    SELECT
        s.id AS stall_id,
        CONCAT('owner_stall_', s.id) AS username
    FROM stalls s
    WHERE s.is_deleted = FALSE
      AND s.created_by_user_id IS NULL
)
UPDATE stalls s
SET
    created_by_user_id = u.id,
    updated_at = NOW()
FROM missing_stalls
JOIN users u ON u.username = missing_stalls.username
WHERE s.id = missing_stalls.stall_id;

COMMIT;
