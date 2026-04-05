-- Canonical schema for FoodStreet Audio Guide
-- Safe to apply on an empty database.

BEGIN;

CREATE TABLE IF NOT EXISTS roles (
    id SERIAL PRIMARY KEY,
    name VARCHAR(50) NOT NULL UNIQUE,
    description TEXT,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS users (
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

CREATE TABLE IF NOT EXISTS languages (
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

CREATE TABLE IF NOT EXISTS categories (
    id SERIAL PRIMARY KEY,
    slug VARCHAR(100) NOT NULL UNIQUE,
    name VARCHAR(120) NOT NULL,
    icon_url TEXT,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS stalls (
    id SERIAL PRIMARY KEY,
    category_id INTEGER REFERENCES categories(id),
    name VARCHAR(200) NOT NULL,
    latitude DOUBLE PRECISION NOT NULL,
    longitude DOUBLE PRECISION NOT NULL,
    image_url TEXT,
    specialty_1 TEXT,
    specialty_2 TEXT,
    specialty_3 TEXT,
    poi_radius_m DOUBLE PRECISION NOT NULL DEFAULT 30,
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

CREATE TABLE IF NOT EXISTS stall_translations (
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

CREATE TABLE IF NOT EXISTS reviews (
    id SERIAL PRIMARY KEY,
    stall_id INTEGER NOT NULL REFERENCES stalls(id) ON DELETE CASCADE,
    rating INTEGER NOT NULL CHECK (rating BETWEEN 1 AND 5),
    ip_address VARCHAR(64),
    comment TEXT,
    reviewer_name VARCHAR(120),
    is_approved BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE TABLE IF NOT EXISTS listening_logs (
    id BIGSERIAL PRIMARY KEY,
    stall_id INTEGER NOT NULL REFERENCES stalls(id) ON DELETE CASCADE,
    language_id INTEGER NOT NULL REFERENCES languages(id),
    session_id VARCHAR(120),
    device_id VARCHAR(120),
    duration_seconds INTEGER NOT NULL DEFAULT 0,
    source VARCHAR(30) NOT NULL DEFAULT 'app',
    latitude DOUBLE PRECISION,
    longitude DOUBLE PRECISION,
    listened_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS stall_audio_assets (
    id SERIAL PRIMARY KEY,
    stall_id INTEGER NOT NULL REFERENCES stalls(id) ON DELETE CASCADE,
    language_id INTEGER NOT NULL REFERENCES languages(id),
    script_hash VARCHAR(64) NOT NULL,
    mime_type VARCHAR(120) NOT NULL DEFAULT 'audio/mpeg',
    audio_data BYTEA NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS stall_update_requests (
    id SERIAL PRIMARY KEY,
    stall_id INTEGER NOT NULL REFERENCES stalls(id) ON DELETE CASCADE,
    submitted_by_user_id INTEGER NOT NULL REFERENCES users(id),
    category_id INTEGER REFERENCES categories(id),
    name VARCHAR(200) NOT NULL,
    latitude DOUBLE PRECISION NOT NULL,
    longitude DOUBLE PRECISION NOT NULL,
    specialty_1 TEXT,
    specialty_2 TEXT,
    specialty_3 TEXT,
    poi_radius_m DOUBLE PRECISION NOT NULL DEFAULT 30,
    opening_hours VARCHAR(255),
    is_open BOOLEAN NOT NULL DEFAULT TRUE,
    script_vi TEXT NOT NULL,
    image_url TEXT,
    status VARCHAR(20) NOT NULL DEFAULT 'pending',
    admin_note TEXT,
    submitted_at TIMESTAMP NOT NULL DEFAULT NOW(),
    reviewed_at TIMESTAMP,
    reviewed_by_user_id INTEGER REFERENCES users(id),
    owner_read_at TIMESTAMP,
    owner_deleted BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE TABLE IF NOT EXISTS location_logs (
    id BIGSERIAL PRIMARY KEY,
    session_id VARCHAR(120),
    device_id VARCHAR(120),
    latitude DOUBLE PRECISION NOT NULL,
    longitude DOUBLE PRECISION NOT NULL,
    source VARCHAR(30) NOT NULL DEFAULT 'app',
    recorded_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_users_role_id ON users(role_id);
CREATE INDEX IF NOT EXISTS ix_stalls_category_id ON stalls(category_id);
CREATE INDEX IF NOT EXISTS ix_stalls_is_active ON stalls(is_active);
CREATE INDEX IF NOT EXISTS ix_stalls_is_deleted ON stalls(is_deleted);
CREATE INDEX IF NOT EXISTS ix_stall_translations_stall_id ON stall_translations(stall_id);
CREATE INDEX IF NOT EXISTS ix_stall_translations_language_id ON stall_translations(language_id);
CREATE INDEX IF NOT EXISTS ix_reviews_stall_id ON reviews(stall_id);
CREATE INDEX IF NOT EXISTS ix_reviews_is_approved_created_at ON reviews(is_approved, created_at);
CREATE INDEX IF NOT EXISTS ix_listening_logs_stall_language_time ON listening_logs(stall_id, language_id, listened_at);
CREATE INDEX IF NOT EXISTS ix_stall_audio_assets_stall_id ON stall_audio_assets(stall_id);
CREATE INDEX IF NOT EXISTS ix_stall_audio_assets_language_id ON stall_audio_assets(language_id);
CREATE INDEX IF NOT EXISTS ix_stall_update_requests_stall_id ON stall_update_requests(stall_id);
CREATE INDEX IF NOT EXISTS ix_stall_update_requests_status ON stall_update_requests(status);

COMMIT;

