CREATE TABLE IF NOT EXISTS restaurant_settings (
    restaurant_id UUID PRIMARY KEY REFERENCES restaurants(id) ON DELETE CASCADE,
    phone VARCHAR(30),
    email VARCHAR(180),
    street VARCHAR(180),
    number VARCHAR(30),
    complement VARCHAR(120),
    neighborhood VARCHAR(120),
    city VARCHAR(120),
    state VARCHAR(2),
    zip_code VARCHAR(12),
    service_fee_percent NUMERIC(5,2) NOT NULL DEFAULT 0,
    opening_hours TEXT,
    logo_url VARCHAR(500),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
