CREATE TABLE IF NOT EXISTS cash_sessions (
    id UUID PRIMARY KEY,
    restaurant_id UUID NOT NULL REFERENCES restaurants(id),
    opened_by_user_id UUID NOT NULL REFERENCES users(id),
    closed_by_user_id UUID NULL REFERENCES users(id),
    status VARCHAR(20) NOT NULL DEFAULT 'OPEN',
    opening_amount NUMERIC(12,2) NOT NULL DEFAULT 0,
    expected_cash_amount NUMERIC(12,2),
    counted_cash_amount NUMERIC(12,2),
    difference_amount NUMERIC(12,2),
    opened_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    closed_at TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_cash_sessions_restaurant_status
    ON cash_sessions(restaurant_id, status);

ALTER TABLE cash_movements
    ADD COLUMN IF NOT EXISTS cash_session_id UUID NULL REFERENCES cash_sessions(id);

ALTER TABLE cash_movements
    ADD COLUMN IF NOT EXISTS order_id UUID NULL REFERENCES orders(id);

CREATE INDEX IF NOT EXISTS idx_cash_movements_session_date
    ON cash_movements(restaurant_id, cash_session_id, created_at);
