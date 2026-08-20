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
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
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
        b.Entity<Recipe>().Property(x => x.Quantity).HasPrecision(14, 3);

        b.Entity<StockMovement>(entity =>
        {
            entity.ToTable("stock_movements");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.RestaurantId).HasColumnName("restaurant_id");
            entity.Property(x => x.IngredientId).HasColumnName("ingredient_id");
            entity.Property(x => x.OrderId).HasColumnName("order_id");
            entity.Property(x => x.Type).HasColumnName("type").HasMaxLength(20).IsRequired();
            entity.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(14, 3);
            entity.Property(x => x.Description).HasColumnName("description").HasMaxLength(240).IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(x => new { x.RestaurantId, x.IngredientId, x.CreatedAt })
                .HasDatabaseName("idx_stock_movements_restaurant_ingredient_date");
        });

        b.Entity<Order>().Property(x => x.Total).HasPrecision(12, 2);
        b.Entity<OrderItem>().Property(x => x.UnitPrice).HasPrecision(12, 2);
        b.Entity<CashMovement>().Property(x => x.Amount).HasPrecision(12, 2);
        b.Entity<AppUser>().HasOne(x => x.Restaurant).WithMany().HasForeignKey(x => x.RestaurantId);
    }
}
