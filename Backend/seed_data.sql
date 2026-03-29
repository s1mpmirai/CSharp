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
    created_at,
    updated_at,
    is_deleted
)
SELECT
    1,
    'Ốc Vĩnh Khánh',
    10.759850,
    106.704750,
    NULL,
    '17:00-23:30',
    TRUE,
    TRUE,
    4.8,
    120,
    NOW(),
    NOW(),
    FALSE
WHERE NOT EXISTS (
    SELECT 1 FROM stalls WHERE name = 'Ốc Vĩnh Khánh'
);

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
    created_at,
    updated_at,
    is_deleted
)
SELECT
    4,
    'Bánh Tráng Nướng Cô Út',
    10.762150,
    106.701920,
    NULL,
    '15:00-22:00',
    TRUE,
    TRUE,
    4.6,
    85,
    NOW(),
    NOW(),
    FALSE
WHERE NOT EXISTS (
    SELECT 1 FROM stalls WHERE name = 'Bánh Tráng Nướng Cô Út'
);

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
    created_at,
    updated_at,
    is_deleted
)
SELECT
    3,
    'Phở Gà Chú Tư',
    10.764120,
    106.698880,
    NULL,
    '06:00-13:30',
    TRUE,
    TRUE,
    4.7,
    64,
    NOW(),
    NOW(),
    FALSE
WHERE NOT EXISTS (
    SELECT 1 FROM stalls WHERE name = 'Phở Gà Chú Tư'
);

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
    'Ốc Vĩnh Khánh',
    'Quán ốc nổi tiếng với nhiều món hải sản đậm vị.',
    'Chào mừng bạn đến với Ốc Vĩnh Khánh, một điểm dừng chân nổi tiếng dành cho tín đồ hải sản đường phố.',
    FALSE,
    'approved',
    1,
    NOW(),
    NOW()
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
SELECT
    s.id,
    l.id,
    'Oc Vinh Khanh',
    'A famous seafood stall with bold street flavors.',
    'Welcome to Oc Vinh Khanh, a well-known stop for street seafood lovers.',
    TRUE,
    'auto_generated',
    1,
    NOW(),
    NOW()
FROM stalls s
JOIN languages l ON l.code = 'en'
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
SELECT
    s.id,
    l.id,
    '永庆螺肉摊',
    '以浓郁风味海鲜闻名的小吃摊。',
    '欢迎来到永庆螺肉摊，这里是街头海鲜爱好者喜爱的热门去处。',
    TRUE,
    'auto_generated',
    1,
    NOW(),
    NOW()
FROM stalls s
JOIN languages l ON l.code = 'zh-CN'
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
SELECT
    s.id,
    l.id,
    'オック・ヴィン・カイン',
    '濃い味付けのシーフードで有名な屋台です。',
    'オック・ヴィン・カインへようこそ。ここはストリートシーフード好きに人気の立ち寄りスポットです。',
    TRUE,
    'auto_generated',
    1,
    NOW(),
    NOW()
FROM stalls s
JOIN languages l ON l.code = 'ja'
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
SELECT
    s.id,
    l.id,
    '옥빈카인',
    '진한 풍미의 해산물로 유명한 길거리 음식점입니다.',
    '옥빈카인에 오신 것을 환영합니다. 이곳은 길거리 해산물을 좋아하는 사람들에게 인기 있는 장소입니다.',
    TRUE,
    'auto_generated',
    1,
    NOW(),
    NOW()
FROM stalls s
JOIN languages l ON l.code = 'ko'
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
SELECT
    s.id,
    l.id,
    'Bánh Tráng Nướng Cô Út',
    'Quầy ăn vặt quen thuộc với món bánh tráng nướng giòn thơm.',
    'Đây là nơi bạn có thể thưởng thức bánh tráng nướng giòn thơm với nhiều loại topping hấp dẫn.',
    FALSE,
    'approved',
    1,
    NOW(),
    NOW()
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
SELECT
    s.id,
    l.id,
    'Pho Ga Chu Tu',
    'A familiar noodle stall serving fragrant chicken pho.',
    'This is a great place to enjoy a warm bowl of fragrant chicken pho in the morning.',
    FALSE,
    'approved',
    1,
    NOW(),
    NOW()
FROM stalls s
JOIN languages l ON l.code = 'en'
WHERE s.name = 'Phở Gà Chú Tư'
ON CONFLICT (stall_id, language_id) DO NOTHING;

COMMIT;
