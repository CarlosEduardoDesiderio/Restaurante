CREATE TABLE IF NOT EXISTS "StockMovements" (
    "Id" uuid PRIMARY KEY,
    "RestaurantId" uuid NOT NULL,
    "IngredientId" uuid NOT NULL,
    "OrderId" uuid NULL,
    "Type" text NOT NULL,
    "Quantity" numeric(14,3) NOT NULL,
    "Description" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);

CREATE INDEX IF NOT EXISTS "IX_StockMovements_RestaurantId_IngredientId_CreatedAt"
    ON "StockMovements" ("RestaurantId", "IngredientId", "CreatedAt");
