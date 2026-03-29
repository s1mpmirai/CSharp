BEGIN;

CREATE TABLE IF NOT EXISTS stall_translations (
    id SERIAL PRIMARY KEY,
    stall_id INTEGER NOT NULL REFERENCES stalls(id) ON DELETE CASCADE,
    language_code VARCHAR(16) NOT NULL,
    script TEXT NOT NULL,
    CONSTRAINT uq_stall_language UNIQUE (stall_id, language_code)
);

CREATE INDEX IF NOT EXISTS ix_stall_translations_stall_id
ON stall_translations (stall_id);

INSERT INTO stall_translations (stall_id, language_code, script)
SELECT id, 'vi', script_vi
FROM stalls
WHERE script_vi IS NOT NULL AND script_vi <> ''
ON CONFLICT (stall_id, language_code) DO NOTHING;

INSERT INTO stall_translations (stall_id, language_code, script)
SELECT id, 'en', script_en
FROM stalls
WHERE script_en IS NOT NULL AND script_en <> ''
ON CONFLICT (stall_id, language_code) DO NOTHING;

INSERT INTO stall_translations (stall_id, language_code, script)
SELECT id, 'ko', script_ko
FROM stalls
WHERE script_ko IS NOT NULL AND script_ko <> ''
ON CONFLICT (stall_id, language_code) DO NOTHING;

INSERT INTO stall_translations (stall_id, language_code, script)
SELECT id, 'ja', script_ja
FROM stalls
WHERE script_ja IS NOT NULL AND script_ja <> ''
ON CONFLICT (stall_id, language_code) DO NOTHING;

INSERT INTO stall_translations (stall_id, language_code, script)
SELECT id, 'zh-CN', script_zh
FROM stalls
WHERE script_zh IS NOT NULL AND script_zh <> ''
ON CONFLICT (stall_id, language_code) DO NOTHING;

COMMIT;
