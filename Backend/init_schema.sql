BEGIN;

DROP TABLE IF EXISTS listening_logs CASCADE;
DROP TABLE IF EXISTS reviews CASCADE;
DROP TABLE IF EXISTS stall_translations CASCADE;
DROP TABLE IF EXISTS stalls CASCADE;
DROP TABLE IF EXISTS categories CASCADE;
DROP TABLE IF EXISTS languages CASCADE;
DROP TABLE IF EXISTS users CASCADE;
DROP TABLE IF EXISTS roles CASCADE;

CREATE TABLE roles (
    id SERIAL PRIMARY KEY,
    name VARCHAR(50) NOT NULL UNIQUE,
    description TEXT,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE TABLE users (
    id SERIAL PRIMARY KEY,
    role_id INTEGER NOT NULL REFERENCES roles(id),
    username VARCHAR(100) NOT NULL UNIQUE,
    password_hash TEXT NOT NULL,
    full_name VARCHAR(150),
    email VARCHAR(150) UNIQUE,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE TABLE languages (
    id SERIAL PRIMARY KEY,
    code VARCHAR(16) NOT NULL UNIQUE,
    name VARCHAR(100) NOT NULL,
    native_name VARCHAR(100) NOT NULL,
    locale_code VARCHAR(20) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    sort_order INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE TABLE categories (
    id SERIAL PRIMARY KEY,
    slug VARCHAR(100) NOT NULL UNIQUE,
    name VARCHAR(120) NOT NULL,
    icon_url TEXT,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE TABLE stalls (
    id SERIAL PRIMARY KEY,
    category_id INTEGER REFERENCES categories(id),
    name VARCHAR(200) NOT NULL,
    latitude DOUBLE PRECISION NOT NULL,
    longitude DOUBLE PRECISION NOT NULL,
    image_url TEXT,
    opening_hours VARCHAR(255),
    is_open BOOLEAN NOT NULL DEFAULT TRUE,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    rating_avg NUMERIC(2,1) NOT NULL DEFAULT 0,
    reviews_count INTEGER NOT NULL DEFAULT 0,
    created_by_user_id INTEGER REFERENCES users(id),
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE TABLE stall_translations (
    id SERIAL PRIMARY KEY,
    stall_id INTEGER NOT NULL REFERENCES stalls(id) ON DELETE CASCADE,
    language_id INTEGER NOT NULL REFERENCES languages(id),
    title VARCHAR(200),
    description TEXT,
    script_text TEXT NOT NULL,
    is_auto_generated BOOLEAN NOT NULL DEFAULT TRUE,
    translation_status VARCHAR(30) NOT NULL DEFAULT 'draft',
    source_version INTEGER NOT NULL DEFAULT 1,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_stall_translation UNIQUE (stall_id, language_id)
);

CREATE TABLE reviews (
    id SERIAL PRIMARY KEY,
    stall_id INTEGER NOT NULL REFERENCES stalls(id) ON DELETE CASCADE,
    rating INTEGER NOT NULL CHECK (rating BETWEEN 1 AND 5),
    comment TEXT,
    reviewer_name VARCHAR(120),
    is_approved BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE TABLE listening_logs (
    id BIGSERIAL PRIMARY KEY,
    stall_id INTEGER NOT NULL REFERENCES stalls(id) ON DELETE CASCADE,
    language_id INTEGER NOT NULL REFERENCES languages(id),
    session_id VARCHAR(120),
    device_id VARCHAR(120),
    duration_seconds INTEGER NOT NULL DEFAULT 0,
    source VARCHAR(30) NOT NULL DEFAULT 'app',
    listened_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX ix_users_role_id ON users(role_id);

CREATE INDEX ix_stalls_category_id ON stalls(category_id);
CREATE INDEX ix_stalls_is_active ON stalls(is_active);
CREATE INDEX ix_stalls_is_deleted ON stalls(is_deleted);

CREATE INDEX ix_stall_translations_stall_id ON stall_translations(stall_id);
CREATE INDEX ix_stall_translations_language_id ON stall_translations(language_id);

CREATE INDEX ix_reviews_stall_id ON reviews(stall_id);
CREATE INDEX ix_reviews_is_approved_created_at ON reviews(is_approved, created_at);

CREATE INDEX ix_listening_logs_stall_language_time
ON listening_logs(stall_id, language_id, listened_at);

INSERT INTO roles (name, description) VALUES
('super_admin', 'Toan quyen he thong'),
('content_admin', 'Quan ly noi dung, gian hang, ban dich')
ON CONFLICT (name) DO NOTHING;

INSERT INTO languages (code, name, native_name, locale_code, sort_order) VALUES
('vi', 'Vietnamese', 'Tiếng Việt', 'vi-VN', 1),
('en', 'English', 'English', 'en-US', 2),
('zh-CN', 'Chinese', '中文', 'zh-CN', 3),
('ja', 'Japanese', '日本語', 'ja-JP', 4),
('ko', 'Korean', '한국어', 'ko-KR', 5)
ON CONFLICT (code) DO NOTHING;

INSERT INTO categories (slug, name, icon_url) VALUES
('seafood', 'Hải sản', NULL),
('grilled', 'Đồ nướng', NULL),
('noodles', 'Món nước', NULL),
('snacks', 'Ăn vặt', NULL),
('desserts', 'Tráng miệng', NULL)
ON CONFLICT (slug) DO NOTHING;

COMMIT;
