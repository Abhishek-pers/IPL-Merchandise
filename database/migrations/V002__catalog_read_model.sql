-- =============================================================================
-- V002 - Denormalised READ model for the catalogue (CQRS-lite).
--
-- Why: the product list / search / details pages are >95% of traffic. They
-- need product + franchise + category in one row. Instead of a 3-way join on
-- every request, we keep a flat, pre-joined table that:
--   * is indexed for every search dimension (name, franchise, category, price)
--   * can be served from a READ REPLICA (see docs/06-distributed-database.md)
--   * can later be pushed to a search engine / cache without touching writes
--
-- Consistency: maintained by triggers on the write tables, so the projection
-- is updated INSIDE THE SAME TRANSACTION as the write (ACID, no lag on the
-- primary). The trade-off (logic in the DB) is recorded in docs/adr/0003.
-- =============================================================================

CREATE TABLE product_catalog (
    product_id      uuid          PRIMARY KEY REFERENCES products(id) ON DELETE CASCADE,
    sku             varchar(40)   NOT NULL,
    name            varchar(200)  NOT NULL,
    description     text          NOT NULL,
    price           numeric(12,2) NOT NULL,
    currency        char(3)       NOT NULL,
    stock_quantity  integer       NOT NULL,
    is_active       boolean       NOT NULL,
    image_url       varchar(500),
    attributes      jsonb         NOT NULL,
    franchise_id    uuid          NOT NULL,
    franchise_code  varchar(8)    NOT NULL,
    franchise_name  varchar(100)  NOT NULL,
    franchise_color char(7)       NOT NULL,
    category_id     uuid          NOT NULL,
    category_code   varchar(40)   NOT NULL,
    category_name   varchar(80)   NOT NULL,
    created_at      timestamptz   NOT NULL,
    updated_at      timestamptz   NOT NULL,
    -- One text column that concatenates every searchable field, so a single
    -- trigram index serves "search by name / type / franchise".
    search_text     text GENERATED ALWAYS AS (
        lower(name || ' ' || sku || ' ' || franchise_code || ' ' || franchise_name || ' ' || category_name)
    ) STORED
);

CREATE INDEX ix_product_catalog_search_trgm ON product_catalog USING gin (search_text gin_trgm_ops);
CREATE INDEX ix_product_catalog_franchise   ON product_catalog (franchise_code) WHERE is_active;
CREATE INDEX ix_product_catalog_category    ON product_catalog (category_code)  WHERE is_active;
CREATE INDEX ix_product_catalog_price       ON product_catalog (price)          WHERE is_active;
CREATE INDEX ix_product_catalog_created     ON product_catalog (created_at DESC) WHERE is_active;

-- ---------------------------------------------------------------- projection
-- Re-projects one product row. Idempotent (upsert), so it is safe to call
-- from any trigger, from a backfill, or repeatedly.
CREATE OR REPLACE FUNCTION project_product(p_product_id uuid) RETURNS void
LANGUAGE sql AS $$
    INSERT INTO product_catalog (
        product_id, sku, name, description, price, currency, stock_quantity, is_active,
        image_url, attributes, franchise_id, franchise_code, franchise_name, franchise_color,
        category_id, category_code, category_name, created_at, updated_at)
    SELECT p.id, p.sku, p.name, p.description, p.price, p.currency, p.stock_quantity, p.is_active,
           p.image_url, p.attributes, f.id, f.code, f.name, f.primary_color,
           c.id, c.code, c.name, p.created_at, p.updated_at
    FROM products p
    JOIN franchises f         ON f.id = p.franchise_id
    JOIN product_categories c ON c.id = p.category_id
    WHERE p.id = p_product_id
    ON CONFLICT (product_id) DO UPDATE SET
        sku = EXCLUDED.sku, name = EXCLUDED.name, description = EXCLUDED.description,
        price = EXCLUDED.price, currency = EXCLUDED.currency,
        stock_quantity = EXCLUDED.stock_quantity, is_active = EXCLUDED.is_active,
        image_url = EXCLUDED.image_url, attributes = EXCLUDED.attributes,
        franchise_id = EXCLUDED.franchise_id, franchise_code = EXCLUDED.franchise_code,
        franchise_name = EXCLUDED.franchise_name, franchise_color = EXCLUDED.franchise_color,
        category_id = EXCLUDED.category_id, category_code = EXCLUDED.category_code,
        category_name = EXCLUDED.category_name, created_at = EXCLUDED.created_at,
        updated_at = EXCLUDED.updated_at;
$$;

-- products INSERT/UPDATE -> re-project that product.
-- (DELETE is handled by ON DELETE CASCADE on product_catalog.product_id.)
CREATE OR REPLACE FUNCTION trg_products_project() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    PERFORM project_product(NEW.id);
    RETURN NULL;
END;
$$;

CREATE TRIGGER products_project
AFTER INSERT OR UPDATE ON products
FOR EACH ROW EXECUTE FUNCTION trg_products_project();

-- Keep updated_at honest without relying on every writer to set it.
CREATE OR REPLACE FUNCTION trg_touch_updated_at() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    NEW.updated_at := now();
    RETURN NEW;
END;
$$;

CREATE TRIGGER products_touch_updated_at
BEFORE UPDATE ON products
FOR EACH ROW EXECUTE FUNCTION trg_touch_updated_at();

-- Franchise renamed / recoloured -> refresh the denormalised columns (set-based).
CREATE OR REPLACE FUNCTION trg_franchises_project() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    UPDATE product_catalog
       SET franchise_code = NEW.code, franchise_name = NEW.name, franchise_color = NEW.primary_color
     WHERE franchise_id = NEW.id;
    RETURN NULL;
END;
$$;

CREATE TRIGGER franchises_project
AFTER UPDATE OF code, name, primary_color ON franchises
FOR EACH ROW EXECUTE FUNCTION trg_franchises_project();

-- Category renamed -> refresh the denormalised columns (set-based).
CREATE OR REPLACE FUNCTION trg_categories_project() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    UPDATE product_catalog
       SET category_code = NEW.code, category_name = NEW.name
     WHERE category_id = NEW.id;
    RETURN NULL;
END;
$$;

CREATE TRIGGER categories_project
AFTER UPDATE OF code, name ON product_categories
FOR EACH ROW EXECUTE FUNCTION trg_categories_project();
