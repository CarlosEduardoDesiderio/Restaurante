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

    // Em desenvolvimento, deixa os produtos demo prontos para testar o fluxo
    // Pedido -> Estoque -> Cozinha -> Pagamento sem montagem manual inicial.
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
                ("Pão de hambúrguer", 1m),
                ("Carne bovina", 180m),
                ("Queijo", 20m),
                ("Molho especial", 15m),
                ("Alface", 15m),
                ("Tomate", 30m)
            };

            foreach (var item in hamburgerRecipe)
            {
                if (ingredientsByName.TryGetValue(item.Name, out var ingredient))
                    db.Recipes.Add(new Recipe { ProductId = hamburger.Id, IngredientId = ingredient.Id, Quantity = item.Quantity });
            }
        }

        var soda = await db.Products
            .SingleOrDefaultAsync(x => x.RestaurantId == demoRestaurant.Id && x.Name == "Refrigerante");
        if (soda is not null && !await db.Recipes.AnyAsync(x => x.ProductId == soda.Id)
            && ingredientsByName.TryGetValue("Refrigerante lata", out var sodaIngredient))
        {
            db.Recipes.Add(new Recipe { ProductId = soda.Id, IngredientId = sodaIngredient.Id, Quantity = 1m });
        }

        await db.SaveChangesAsync();
    }
}

await app.RunAsync();
