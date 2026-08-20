CREATE TABLE IF NOT EXISTS financial_entries (
    id UUID PRIMARY KEY,
    restaurant_id UUID NOT NULL REFERENCES restaurants(id),
    type VARCHAR(20) NOT NULL,
    category VARCHAR(60) NOT NULL,
    description VARCHAR(240) NOT NULL,
    amount NUMERIC(12,2) NOT NULL,
    due_date TIMESTAMPTZ NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'PENDING',
    payment_method VARCHAR(30),
    paid_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_financial_entries_filter
    ON financial_entries(restaurant_id, type, status, due_date);
