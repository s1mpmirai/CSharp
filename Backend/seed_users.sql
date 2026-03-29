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
SELECT r.id, 'admin', '240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9',
       'Quản trị hệ thống', 'admin@streetfeast.local', TRUE, NOW(), NOW()
FROM roles r
WHERE r.name = 'super_admin'
ON CONFLICT (username) DO NOTHING;

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
SELECT r.id, 'chuoc', '43a0d17178a9d26c9e0fe9a74b0b45e38d32f27aed887a008a54bf6e033bf7b9',
       'Chủ quán Ốc Vĩnh Khánh', 'chuoc@streetfeast.local', TRUE, NOW(), NOW()
FROM roles r
WHERE r.name = 'stall_owner'
ON CONFLICT (username) DO NOTHING;

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
SELECT r.id, 'chubanhtrang', '43a0d17178a9d26c9e0fe9a74b0b45e38d32f27aed887a008a54bf6e033bf7b9',
       'Chủ quán Bánh Tráng Nướng', 'chubanhtrang@streetfeast.local', TRUE, NOW(), NOW()
FROM roles r
WHERE r.name = 'stall_owner'
ON CONFLICT (username) DO NOTHING;

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
SELECT r.id, 'chupho', '43a0d17178a9d26c9e0fe9a74b0b45e38d32f27aed887a008a54bf6e033bf7b9',
       'Chủ quán Phở Gà Chú Tư', 'chupho@streetfeast.local', TRUE, NOW(), NOW()
FROM roles r
WHERE r.name = 'stall_owner'
ON CONFLICT (username) DO NOTHING;

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
SELECT r.id, 'chukem', '43a0d17178a9d26c9e0fe9a74b0b45e38d32f27aed887a008a54bf6e033bf7b9',
       'Chủ quán Kem Dừa', 'chukem@streetfeast.local', TRUE, NOW(), NOW()
FROM roles r
WHERE r.name = 'stall_owner'
ON CONFLICT (username) DO NOTHING;

COMMIT;
