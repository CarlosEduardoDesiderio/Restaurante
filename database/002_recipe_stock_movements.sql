CREATE TABLE IF NOT EXISTS stock_movements (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    restaurant_id UUID NOT NULL REFERENCES restaurants(id),
    ingredient_id UUID NOT NULL REFERENCES ingredients(id),
    order_id UUID NULL REFERENCES orders(id) ON DELETE SET NULL,
    type VARCHAR(20) NOT NULL,
    quantity NUMERIC(14,3) NOT NULL,
    description VARCHAR(240) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_stock_movements_restaurant_ingredient_date
    ON stock_movements(restaurant_id, ingredient_id, created_at);
