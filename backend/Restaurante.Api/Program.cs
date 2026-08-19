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
    o.TokenValidationParameters = new TokenValidationParameters { ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(key), ValidateIssuer = false, ValidateAudience = false };
});
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseSwagger(); app.UseSwaggerUI(); app.UseCors("frontend"); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "UP", version = "1.0.0" }));

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    if (!await db.Restaurants.AnyAsync())
    {
        var restaurant = new Restaurant { Name = "Restaurante Demonstração" };
        db.Restaurants.Add(restaurant);
        db.Users.Add(new AppUser { RestaurantId = restaurant.Id, Name = "Administrador", Email = "admin@demo.local", Role = "ADMIN", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123") });
        db.Categories.AddRange(new Category { RestaurantId = restaurant.Id, Name = "Hambúrgueres" }, new Category { RestaurantId = restaurant.Id, Name = "Bebidas" });
        db.Products.AddRange(new Product { RestaurantId = restaurant.Id, Name = "Hambúrguer Artesanal", Description = "Pão, carne, queijo e molho", Price = 29.90m }, new Product { RestaurantId = restaurant.Id, Name = "Refrigerante", Price = 7.00m });
        db.Ingredients.AddRange(new Ingredient { RestaurantId = restaurant.Id, Name = "Carne", Unit = "g", CurrentQuantity = 10000, MinimumQuantity = 2000, CostPerUnit = 0.035m }, new Ingredient { RestaurantId = restaurant.Id, Name = "Queijo", Unit = "g", CurrentQuantity = 1000, MinimumQuantity = 200, CostPerUnit = 0.04m });
        for (var i = 1; i <= 12; i++) db.Tables.Add(new RestaurantTable { RestaurantId = restaurant.Id, Number = i, Seats = 4 });
        await db.SaveChangesAsync();
    }
}
await app.RunAsync();
