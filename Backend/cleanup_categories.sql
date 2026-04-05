-- Normalize duplicated / mojibake categories without losing stall or owner data.
-- Safe for an existing database with live data.

BEGIN;

CREATE TEMP TABLE tmp_category_canonical (
    canonical_slug VARCHAR(100) PRIMARY KEY,
    canonical_name VARCHAR(120) NOT NULL
);

INSERT INTO tmp_category_canonical (canonical_slug, canonical_name)
VALUES
    ('seafood', 'Hải sản'),
    ('grilled', 'Đồ nướng'),
    ('noodles', 'Món nước'),
    ('snacks', 'Ăn vặt'),
    ('desserts', 'Tráng miệng'),
    ('rice', 'Cơm'),
    ('dumplings', 'Há cảo'),
    ('specialties', 'Đặc sản');

INSERT INTO categories (slug, name, is_active, created_at, updated_at)
SELECT canonical_slug, canonical_name, TRUE, NOW(), NOW()
FROM tmp_category_canonical
ON CONFLICT (slug) DO UPDATE
SET
    name = EXCLUDED.name,
    is_active = TRUE,
    updated_at = NOW();

CREATE TEMP TABLE tmp_category_alias (
    alias_slug VARCHAR(100),
    alias_name VARCHAR(120),
    canonical_slug VARCHAR(100) NOT NULL
);

INSERT INTO tmp_category_alias (alias_slug, alias_name, canonical_slug)
VALUES
    ('cat-1', NULL, 'seafood'),
    ('seafood', 'H?i s?n', 'seafood'),
    ('seafood', 'Hải sản', 'seafood'),
    ('cat-2', NULL, 'grilled'),
    ('grilled', 'Ð? nu?ng', 'grilled'),
    ('grilled', 'Đồ nướng', 'grilled'),
    ('cat-3', NULL, 'noodles'),
    ('noodles', 'Món nu?c', 'noodles'),
    ('noodles', 'Món nước', 'noodles'),
    ('cat-4', NULL, 'snacks'),
    ('snacks', 'An v?t', 'snacks'),
    ('snacks', 'Ăn vặt', 'snacks'),
    ('cat-5', NULL, 'desserts'),
    ('desserts', 'Tráng mi?ng', 'desserts'),
    ('desserts', 'Tráng miệng', 'desserts'),
    ('cat-6', NULL, 'rice'),
    ('rice', 'Com', 'rice'),
    ('rice', 'Cơm', 'rice'),
    ('cat-7', NULL, 'dumplings'),
    ('dumplings', 'Há c?o', 'dumplings'),
    ('dumplings', 'Há cảo', 'dumplings'),
    ('cat-8', NULL, 'specialties'),
    ('specialties', 'Ð?c s?n', 'specialties'),
    ('specialties', 'Đặc sản', 'specialties');

CREATE TEMP TABLE tmp_category_map AS
SELECT DISTINCT
    source.id AS source_id,
    canonical.id AS target_id
FROM categories source
JOIN tmp_category_alias alias
    ON (
        (alias.alias_slug IS NOT NULL AND source.slug = alias.alias_slug)
        OR (alias.alias_name IS NOT NULL AND source.name = alias.alias_name)
    )
JOIN categories canonical
    ON canonical.slug = alias.canonical_slug
WHERE source.id <> canonical.id;

UPDATE stalls s
SET category_id = map.target_id
FROM tmp_category_map map
WHERE s.category_id = map.source_id;

UPDATE stall_update_requests r
SET category_id = map.target_id
FROM tmp_category_map map
WHERE r.category_id = map.source_id;

DELETE FROM categories c
USING tmp_category_map map
WHERE c.id = map.source_id
  AND NOT EXISTS (SELECT 1 FROM stalls s WHERE s.category_id = c.id)
  AND NOT EXISTS (SELECT 1 FROM stall_update_requests r WHERE r.category_id = c.id);

UPDATE categories c
SET
    name = canonical.canonical_name,
    is_active = TRUE,
    updated_at = NOW()
FROM tmp_category_canonical canonical
WHERE c.slug = canonical.canonical_slug;

COMMIT;
