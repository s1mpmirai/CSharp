BEGIN;

CREATE TABLE IF NOT EXISTS stall_update_requests (
    id SERIAL PRIMARY KEY,
    stall_id INTEGER NOT NULL REFERENCES stalls(id) ON DELETE CASCADE,
    submitted_by_user_id INTEGER NOT NULL REFERENCES users(id),
    category_id INTEGER REFERENCES categories(id),
    name VARCHAR(200) NOT NULL,
    latitude DOUBLE PRECISION NOT NULL,
    longitude DOUBLE PRECISION NOT NULL,
    opening_hours VARCHAR(255),
    is_open BOOLEAN NOT NULL DEFAULT TRUE,
    script_vi TEXT NOT NULL,
    image_url TEXT,
    status VARCHAR(20) NOT NULL DEFAULT 'pending',
    admin_note TEXT,
    submitted_at TIMESTAMP NOT NULL DEFAULT NOW(),
    reviewed_at TIMESTAMP,
    reviewed_by_user_id INTEGER REFERENCES users(id)
);

CREATE INDEX IF NOT EXISTS ix_stall_update_requests_stall_id
ON stall_update_requests (stall_id);

CREATE INDEX IF NOT EXISTS ix_stall_update_requests_status
ON stall_update_requests (status);

COMMIT;
