-- =============================================================================
-- V003 - Reference data that every environment needs (prod included).
-- Demo data (products, customers) lives in database/seed and is only applied
-- when Database:SeedDemoData = true.
-- =============================================================================

INSERT INTO franchises (id, code, name, city, primary_color) VALUES
    ('a0000000-0000-0000-0000-000000000001', 'CSK',  'Chennai Super Kings',         'Chennai',   '#F9CD05'),
    ('a0000000-0000-0000-0000-000000000002', 'MI',   'Mumbai Indians',              'Mumbai',    '#004BA0'),
    ('a0000000-0000-0000-0000-000000000003', 'RCB',  'Royal Challengers Bengaluru', 'Bengaluru', '#EC1C24'),
    ('a0000000-0000-0000-0000-000000000004', 'KKR',  'Kolkata Knight Riders',       'Kolkata',   '#3A225D'),
    ('a0000000-0000-0000-0000-000000000005', 'SRH',  'Sunrisers Hyderabad',         'Hyderabad', '#FF822A'),
    ('a0000000-0000-0000-0000-000000000006', 'DC',   'Delhi Capitals',              'Delhi',     '#17479E'),
    ('a0000000-0000-0000-0000-000000000007', 'PBKS', 'Punjab Kings',                'Mohali',    '#ED1B24'),
    ('a0000000-0000-0000-0000-000000000008', 'RR',   'Rajasthan Royals',            'Jaipur',    '#EA1A85'),
    ('a0000000-0000-0000-0000-000000000009', 'GT',   'Gujarat Titans',              'Ahmedabad', '#1B2133'),
    ('a0000000-0000-0000-0000-000000000010', 'LSG',  'Lucknow Super Giants',        'Lucknow',   '#A72056');

INSERT INTO product_categories (id, code, name) VALUES
    ('c0000000-0000-0000-0000-000000000001', 'JERSEY',            'Jersey'),
    ('c0000000-0000-0000-0000-000000000002', 'CAP',               'Cap'),
    ('c0000000-0000-0000-0000-000000000003', 'FLAG',              'Flag'),
    ('c0000000-0000-0000-0000-000000000004', 'AUTOGRAPHED_PHOTO', 'Autographed Photo'),
    ('c0000000-0000-0000-0000-000000000005', 'ACCESSORY',         'Accessory');
