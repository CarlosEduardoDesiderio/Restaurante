using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Restaurante.Api.Data;
using Restaurante.Api.Models;
using Restaurante.Api.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddSingleton<CacheService>();
builder.Services.AddSingleton<JwtService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(o => o.AddPolicy("frontend", p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var key = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "development-only-change-this-secret-very-long-key");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false
    };
});
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "UP", version = "1.0.0" }));

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();

    await db.Database.ExecuteSqlRawAsync("""
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
        ALTER TABLE cash_movements ADD COLUMN IF NOT EXISTS cash_session_id UUID NULL REFERENCES cash_sessions(id);
        ALTER TABLE cash_movements ADD COLUMN IF NOT EXISTS order_id UUID NULL REFERENCES orders(id);
        CREATE INDEX IF NOT EXISTS idx_cash_movements_session_date
            ON cash_movements(restaurant_id, cash_session_id, created_at);

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
        ALTER TABLE orders ADD COLUMN IF NOT EXISTS coupon_id UUID NULL REFERENCES coupons(id) ON DELETE SET NULL;
        ALTER TABLE orders ADD COLUMN IF NOT EXISTS original_total NUMERIC(12,2);
        ALTER TABLE orders ADD COLUMN IF NOT EXISTS discount_amount NUMERIC(12,2) NOT NULL DEFAULT 0;
        ALTER TABLE orders ADD COLUMN IF NOT EXISTS cashback_used NUMERIC(12,2) NOT NULL DEFAULT 0;
        ALTER TABLE orders ADD COLUMN IF NOT EXISTS coupon_code VARCHAR(40);
        CREATE INDEX IF NOT EXISTS idx_orders_customer ON orders(restaurant_id, customer_id, created_at);
        """);

    Restaurant demoRestaurant;

    if (!await db.Restaurants.AnyAsync())
    {
        demoRestaurant = new Restaurant { Name = "Restaurante Demonstração" };
        db.Restaurants.Add(demoRestaurant);
        await db.SaveChangesAsync();

        db.Categories.AddRange(
            new Category { RestaurantId = demoRestaurant.Id, Name = "Hambúrgueres" },
            new Category { RestaurantId = demoRestaurant.Id, Name = "Bebidas" }
        );
        db.Products.AddRange(
            new Product { RestaurantId = demoRestaurant.Id, Name = "Hambúrguer Artesanal", Description = "Pão, carne, queijo e molho", Price = 29.90m },
            new Product { RestaurantId = demoRestaurant.Id, Name = "Refrigerante", Price = 7.00m }
        );
        for (var i = 1; i <= 12; i++)
            db.Tables.Add(new RestaurantTable { RestaurantId = demoRestaurant.Id, Number = i, Seats = 4 });

        await db.SaveChangesAsync();
    }
    else
    {
        demoRestaurant = await db.Restaurants.OrderBy(x => x.Id).FirstAsync();
    }

    if (app.Environment.IsDevelopment())
    {
        var demoAdmin = await db.Users.SingleOrDefaultAsync(x => x.Email == "admin@demo.local");
        if (demoAdmin is null)
        {
            db.Users.Add(new AppUser
            {
                RestaurantId = demoRestaurant.Id,
                Name = "Administrador",
                Email = "admin@demo.local",
                Role = "ADMIN",
                Active = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123")
            });
        }
        else
        {
            demoAdmin.RestaurantId = demoRestaurant.Id;
            demoAdmin.Name = "Administrador";
            demoAdmin.Role = "ADMIN";
            demoAdmin.Active = true;
            demoAdmin.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123");
        }
        await db.SaveChangesAsync();
    }

    var existingIngredientNames = await db.Ingredients
        .Where(x => x.RestaurantId == demoRestaurant.Id)
        .Select(x => x.Name)
        .ToListAsync();

    var demoIngredients = new[]
    {
        new Ingredient { RestaurantId = demoRestaurant.Id, Name = "Pão de hambúrguer", Unit = "un", CurrentQuantity = 100, MinimumQuantity = 20, CostPerUnit = 1.20m },
        new Ingredient { RestaurantId = demoRestaurant.Id, Name = "Carne bovina", Unit = "g", CurrentQuantity = 10000, MinimumQuantity = 2000, CostPerUnit = 0.035m },
        new Ingredient { RestaurantId = demoRestaurant.Id, Name = "Queijo", Unit = "g", CurrentQuantity = 3000, MinimumQuantity = 500, CostPerUnit = 0.04m },
        new Ingredient { RestaurantId = demoRestaurant.Id, Name = "Molho especial", Unit = "g", CurrentQuantity = 2000, MinimumQuantity = 300, CostPerUnit = 0.018m },
        new Ingredient { RestaurantId = demoRestaurant.Id, Name = "Alface", Unit = "g", CurrentQuantity = 1500, MinimumQuantity = 250, CostPerUnit = 0.012m },
        new Ingredient { RestaurantId = demoRestaurant.Id, Name = "Tomate", Unit = "g", CurrentQuantity = 3000, MinimumQuantity = 500, CostPerUnit = 0.009m },
        new Ingredient { RestaurantId = demoRestaurant.Id, Name = "Cebola", Unit = "g", CurrentQuantity = 2000, MinimumQuantity = 300, CostPerUnit = 0.006m },
        new Ingredient { RestaurantId = demoRestaurant.Id, Name = "Bacon", Unit = "g", CurrentQuantity = 2500, MinimumQuantity = 400, CostPerUnit = 0.045m },
        new Ingredient { RestaurantId = demoRestaurant.Id, Name = "Batata congelada", Unit = "g", CurrentQuantity = 8000, MinimumQuantity = 1500, CostPerUnit = 0.014m },
        new Ingredient { RestaurantId = demoRestaurant.Id, Name = "Óleo", Unit = "ml", CurrentQuantity = 5000, MinimumQuantity = 1000, CostPerUnit = 0.008m },
        new Ingredient { RestaurantId = demoRestaurant.Id, Name = "Sal", Unit = "g", CurrentQuantity = 2000, MinimumQuantity = 300, CostPerUnit = 0.002m },
        new Ingredient { RestaurantId = demoRestaurant.Id, Name = "Refrigerante lata", Unit = "un", CurrentQuantity = 120, MinimumQuantity = 24, CostPerUnit = 3.50m }
    };

    var missingIngredients = demoIngredients
        .Where(x => !existingIngredientNames.Contains(x.Name, StringComparer.OrdinalIgnoreCase))
        .ToList();

    if (missingIngredients.Count > 0)
    {
        db.Ingredients.AddRange(missingIngredients);
        await db.SaveChangesAsync();
    }

    if (app.Environment.IsDevelopment())
    {
        var ingredientsByName = await db.Ingredients
            .Where(x => x.RestaurantId == demoRestaurant.Id)
            .ToDictionaryAsync(x => x.Name, StringComparer.OrdinalIgnoreCase);

        var hamburger = await db.Products
            .SingleOrDefaultAsync(x => x.RestaurantId == demoRestaurant.Id && x.Name == "Hambúrguer Artesanal");
        if (hamburger is not null && !await db.Recipes.AnyAsync(x => x.ProductId == hamburger.Id))
        {
            var hamburgerRecipe = new (string Name, decimal Quantity)[]
            {
                ("Pão de hambúrguer", 1m), ("Carne bovina", 180m), ("Queijo", 20m),
                ("Molho especial", 15m), ("Alface", 15m), ("Tomate", 30m)
            };
            foreach (var item in hamburgerRecipe)
                if (ingredientsByName.TryGetValue(item.Name, out var ingredient))
                    db.Recipes.Add(new Recipe { ProductId = hamburger.Id, IngredientId = ingredient.Id, Quantity = item.Quantity });
        }

        var soda = await db.Products
            .SingleOrDefaultAsync(x => x.RestaurantId == demoRestaurant.Id && x.Name == "Refrigerante");
        if (soda is not null && !await db.Recipes.AnyAsync(x => x.ProductId == soda.Id)
            && ingredientsByName.TryGetValue("Refrigerante lata", out var sodaIngredient))
            db.Recipes.Add(new Recipe { ProductId = soda.Id, IngredientId = sodaIngredient.Id, Quantity = 1m });

        await db.SaveChangesAsync();
    }
}

await app.RunAsync();
