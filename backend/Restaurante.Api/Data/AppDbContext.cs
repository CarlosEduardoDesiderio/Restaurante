using Microsoft.EntityFrameworkCore;
using Restaurante.Api.Models;

namespace Restaurante.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Restaurant> Restaurants => Set<Restaurant>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RestaurantTable> Tables => Set<RestaurantTable>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<CashMovement> CashMovements => Set<CashMovement>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppUser>().HasIndex(x => x.Email).IsUnique();
        b.Entity<RestaurantTable>().HasIndex(x => new { x.RestaurantId, x.Number }).IsUnique();
        b.Entity<Product>().Property(x => x.Price).HasPrecision(12, 2);
        b.Entity<Ingredient>().Property(x => x.CurrentQuantity).HasPrecision(14, 3);
        b.Entity<Ingredient>().Property(x => x.MinimumQuantity).HasPrecision(14, 3);
        b.Entity<Ingredient>().Property(x => x.CostPerUnit).HasPrecision(12, 4);
        b.Entity<Recipe>().HasKey(x => new { x.ProductId, x.IngredientId });
        b.Entity<Order>().Property(x => x.Total).HasPrecision(12, 2);
        b.Entity<OrderItem>().Property(x => x.UnitPrice).HasPrecision(12, 2);
        b.Entity<CashMovement>().Property(x => x.Amount).HasPrecision(12, 2);
        b.Entity<AppUser>().HasOne(x => x.Restaurant).WithMany().HasForeignKey(x => x.RestaurantId);
    }
}
