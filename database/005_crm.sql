CREATE TABLE IF NOT EXISTS customers (
    id UUID PRIMARY KEY,
    restaurant_id UUID NOT NULL REFERENCES restaurants(id),
    name VARCHAR(160) NOT NULL,
    phone VARCHAR(30),
    email VARCHAR(180),
    birth_date DATE,
    points INTEGER NOT NULL DEFAULT 0,
    cashback_balance NUMERIC(12,2) NOT NULL DEFAULT 0,
    active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_customers_restaurant_phone ON customers(restaurant_id, phone);

CREATE TABLE IF NOT EXISTS loyalty_movements (
    id UUID PRIMARY KEY,
    restaurant_id UUID NOT NULL REFERENCES restaurants(id),
    customer_id UUID NOT NULL REFERENCES customers(id) ON DELETE CASCADE,
    order_id UUID NULL REFERENCES orders(id) ON DELETE SET NULL,
    type VARCHAR(20) NOT NULL,
    points INTEGER NOT NULL DEFAULT 0,
    cashback NUMERIC(12,2) NOT NULL DEFAULT 0,
    description VARCHAR(240) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_loyalty_customer_date ON loyalty_movements(restaurant_id, customer_id, created_at);

CREATE TABLE IF NOT EXISTS coupons (
    id UUID PRIMARY KEY,
    restaurant_id UUID NOT NULL REFERENCES restaurants(id),
    code VARCHAR(40) NOT NULL,
    description VARCHAR(180) NOT NULL,
    discount_type VARCHAR(20) NOT NULL,
    value NUMERIC(12,2) NOT NULL,
    minimum_order_value NUMERIC(12,2) NOT NULL DEFAULT 0,
    max_uses INTEGER,
    uses INTEGER NOT NULL DEFAULT 0,
    expires_at TIMESTAMPTZ,
    active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(restaurant_id, code)
);

ALTER TABLE orders ADD COLUMN IF NOT EXISTS customer_id UUID NULL REFERENCES customers(id) ON DELETE SET NULL;
CREATE INDEX IF NOT EXISTS idx_orders_customer ON orders(restaurant_id, customer_id, created_at);
