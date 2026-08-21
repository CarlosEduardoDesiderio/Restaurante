CREATE TABLE IF NOT EXISTS delivery_zones (
    id UUID PRIMARY KEY,
    restaurant_id UUID NOT NULL REFERENCES restaurants(id),
    name VARCHAR(120) NOT NULL,
    fee NUMERIC(12,2) NOT NULL DEFAULT 0,
    estimated_minutes INTEGER NOT NULL DEFAULT 45,
    active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE UNIQUE INDEX IF NOT EXISTS delivery_zones_restaurant_name_key ON delivery_zones(restaurant_id, name);

CREATE TABLE IF NOT EXISTS delivery_drivers (
    id UUID PRIMARY KEY,
    restaurant_id UUID NOT NULL REFERENCES restaurants(id),
    name VARCHAR(160) NOT NULL,
    phone VARCHAR(30),
    vehicle VARCHAR(80),
    active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS idx_delivery_drivers_restaurant_active ON delivery_drivers(restaurant_id, active);

CREATE TABLE IF NOT EXISTS delivery_orders (
    id UUID PRIMARY KEY,
    restaurant_id UUID NOT NULL REFERENCES restaurants(id),
    order_id UUID NOT NULL UNIQUE REFERENCES orders(id) ON DELETE CASCADE,
    zone_id UUID NOT NULL REFERENCES delivery_zones(id),
    driver_id UUID NULL REFERENCES delivery_drivers(id),
    address VARCHAR(240) NOT NULL,
    number VARCHAR(30),
    complement VARCHAR(120),
    neighborhood VARCHAR(120) NOT NULL,
    reference VARCHAR(180),
    delivery_fee NUMERIC(12,2) NOT NULL DEFAULT 0,
    status VARCHAR(30) NOT NULL DEFAULT 'WAITING',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    dispatched_at TIMESTAMPTZ,
    delivered_at TIMESTAMPTZ
);
CREATE INDEX IF NOT EXISTS idx_delivery_orders_restaurant_status ON delivery_orders(restaurant_id, status, created_at);
