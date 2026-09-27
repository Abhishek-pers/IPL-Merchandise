-- =============================================================================
-- S001 - Demo catalogue + demo customers (local / dev / test only).
--
-- Idempotent: safe to run on every start-up (ON CONFLICT DO NOTHING).
-- IDs are deterministic (md5 -> uuid) so tests and docs can refer to them.
-- =============================================================================

INSERT INTO customers (id, email, full_name) VALUES
    ('11111111-1111-1111-1111-111111111111', 'aarav.sharma@example.com', 'Aarav Sharma'),
    ('22222222-2222-2222-2222-222222222222', 'priya.nair@example.com',   'Priya Nair')
ON CONFLICT DO NOTHING;

-- 10 franchises x 6 product templates = 60 products.
WITH templates (category_code, sku_suffix, name_suffix, price, stock, description, attributes) AS (
    VALUES
    ('JERSEY',            'JER-H', 'Official Home Jersey 2026', 2499.00, 120,
        'Official match-day home jersey, breathable dri-fit fabric.',
        '{"sizes":["S","M","L","XL","XXL"],"material":"Polyester dri-fit"}'),
    ('JERSEY',            'JER-A', 'Official Away Jersey 2026', 2299.00, 80,
        'Official away jersey worn for away fixtures.',
        '{"sizes":["S","M","L","XL"],"material":"Polyester dri-fit"}'),
    ('CAP',               'CAP',   'Team Cap',                   799.00, 200,
        'Adjustable six-panel cap with embroidered team crest.',
        '{"fit":"Adjustable","material":"Cotton twill"}'),
    ('FLAG',              'FLG',   'Fan Flag (3x2 ft)',          499.00, 300,
        'Double-stitched stadium flag in team colours.',
        '{"dimensions":"3x2 ft","material":"Satin"}'),
    ('AUTOGRAPHED_PHOTO', 'AUT',   'Captain''s Autographed Photo (A4, framed)', 4999.00, 15,
        'Limited edition framed photograph signed by the team captain, with certificate of authenticity.',
        '{"signed_by":"Team Captain","frame":"Black wood","certificate":true}'),
    ('ACCESSORY',         'MUG',   'Coffee Mug',                 399.00, 150,
        'Ceramic 330 ml mug, dishwasher safe.',
        '{"capacity_ml":330,"material":"Ceramic"}')
)
INSERT INTO products (id, sku, name, description, franchise_id, category_id, price, currency,
                      stock_quantity, image_url, attributes)
SELECT md5('product:' || f.code || ':' || t.sku_suffix)::uuid,
       f.code || '-' || t.sku_suffix,
       f.name || ' ' || t.name_suffix,
       t.description,
       f.id,
       c.id,
       t.price,
       'INR',
       t.stock,
       NULL,
       t.attributes::jsonb
FROM franchises f
CROSS JOIN templates t
JOIN product_categories c ON c.code = t.category_code
ON CONFLICT DO NOTHING;
