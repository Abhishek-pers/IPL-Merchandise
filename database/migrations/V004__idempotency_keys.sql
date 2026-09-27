-- =============================================================================
-- V004 - Idempotency keys for non-idempotent commands (Idempotent Receiver).
--
-- Why: "add to cart" INCREMENTS a quantity, so running it twice changes the
-- result. Clients retry on network errors (at-least-once delivery), and the
-- server's own transaction retry can re-run a command whose commit actually
-- succeeded. Recording the key IN THE SAME TRANSACTION as the change makes the
-- effect exactly-once: the key and the change commit together or not at all,
-- and a repeat finds the key and does nothing.
--
-- Checkout does not use this table: its key lives on orders
-- (uq_orders_customer_idempotency) because the replay must return that order.
-- =============================================================================

CREATE TABLE idempotency_keys (
    customer_id      uuid         NOT NULL REFERENCES customers(id),
    operation        varchar(40)  NOT NULL,          -- namespace, e.g. 'cart.add-item'
    idempotency_key  varchar(100) NOT NULL,
    created_at       timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT pk_idempotency_keys PRIMARY KEY (customer_id, operation, idempotency_key)
);

-- Keys only need to outlive the client's retry window. A scheduled job can run:
--   DELETE FROM idempotency_keys WHERE created_at < now() - interval '7 days';
CREATE INDEX ix_idempotency_keys_created ON idempotency_keys (created_at);
