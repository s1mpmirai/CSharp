BEGIN;

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
SELECT r.id, 'admin', 'pbkdf2_sha256$390000$SHGU1N51AvWzZqNDolhXeA$JKlMGBHNFqXIXeM2SJU08lbOJneu_JUwqq9tg-K_aRs',
       'Quản trị hệ thống', 'admin@streetfeast.local', TRUE, NOW(), NOW()
FROM roles r
WHERE r.name = 'super_admin'
ON CONFLICT (username) DO NOTHING;

COMMIT;
