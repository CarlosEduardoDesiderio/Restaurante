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
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<LoyaltyMovement> LoyaltyMovements => Set<LoyaltyMovement>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<CashSession> CashSessions => Set<CashSession>();
    public DbSet<CashMovement> CashMovements => Set<CashMovement>();
    public DbSet<FinancialEntry> FinancialEntries => Set<FinancialEntry>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Restaurant>(entity =>
        {
            entity.ToTable("restaurants"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id"); entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(160).IsRequired(); entity.Property(x => x.Document).HasColumnName("document").HasMaxLength(30); entity.Property(x => x.Active).HasColumnName("active");
        });
        b.Entity<AppUser>(entity =>
        {
            entity.ToTable("users"); entity.HasKey(x => x.Id); entity.Property(x => x.Id).HasColumnName("id"); entity.Property(x => x.RestaurantId).HasColumnName("restaurant_id"); entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired(); entity.Property(x => x.Email).HasColumnName("email").HasMaxLength(180).IsRequired(); entity.Property(x => x.PasswordHash).HasColumnName("password_hash").IsRequired(); entity.Property(x => x.Role).HasColumnName("role").HasMaxLength(30).IsRequired(); entity.Property(x => x.Active).HasColumnName("active"); entity.HasIndex(x => x.Email).IsUnique().HasDatabaseName("users_email_key"); entity.HasOne(x => x.Restaurant).WithMany().HasForeignKey(x => x.RestaurantId);
        });
        b.Entity<Category>(entity =>
        {
            entity.ToTable("categories"); entity.HasKey(x => x.Id); entity.Property(x => x.Id).HasColumnName("id"); entity.Property(x => x.RestaurantId).HasColumnName("restaurant_id"); entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired(); entity.Property(x => x.Active).HasColumnName("active");
        });
        b.Entity<Product>(entity =>
        {
            entity.ToTable("products"); entity.HasKey(x => x.Id); entity.Property(x => x.Id).HasColumnName("id"); entity.Property(x => x.RestaurantId).HasColumnName("restaurant_id"); entity.Property(x => x.CategoryId).HasColumnName("category_id"); entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(160).IsRequired(); entity.Property(x => x.Description).HasColumnName("description"); entity.Property(x => x.Price).HasColumnName("price").HasPrecision(12, 2); entity.Property(x => x.Active).HasColumnName("active");
        });
        b.Entity<Ingredient>(entity =>
        {
            entity.ToTable("ingredients"); entity.HasKey(x => x.Id); entity.Property(x => x.Id).HasColumnName("id"); entity.Property(x => x.RestaurantId).HasColumnName("restaurant_id"); entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(160).IsRequired(); entity.Property(x => x.Unit).HasColumnName("unit").HasMaxLength(20).IsRequired(); entity.Property(x => x.CurrentQuantity).HasColumnName("current_quantity").HasPrecision(14, 3); entity.Property(x => x.MinimumQuantity).HasColumnName("minimum_quantity").HasPrecision(14, 3); entity.Property(x => x.CostPerUnit).HasColumnName("cost_per_unit").HasPrecision(12, 4); entity.Property(x => x.ExpirationDate).HasColumnName("expiration_date");
        });
        b.Entity<Recipe>(entity =>
        {
            entity.ToTable("recipes"); entity.HasKey(x => new { x.ProductId, x.IngredientId }); entity.Property(x => x.ProductId).HasColumnName("product_id"); entity.Property(x => x.IngredientId).HasColumnName("ingredient_id"); entity.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(14, 3);
        });
        b.Entity<StockMovement>(entity =>
        {
            entity.ToTable("stock_movements"); entity.HasKey(x => x.Id); entity.Property(x => x.Id).HasColumnName("id"); entity.Property(x => x.RestaurantId).HasColumnName("restaurant_id"); entity.Property(x => x.IngredientId).HasColumnName("ingredient_id"); entity.Property(x => x.OrderId).HasColumnName("order_id"); entity.Property(x => x.Type).HasColumnName("type").HasMaxLength(20).IsRequired(); entity.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(14, 3); entity.Property(x => x.Description).HasColumnName("description").HasMaxLength(240).IsRequired(); entity.Property(x => x.CreatedAt).HasColumnName("created_at"); entity.HasIndex(x => new { x.RestaurantId, x.IngredientId, x.CreatedAt }).HasDatabaseName("idx_stock_movements_restaurant_ingredient_date");
        });
        b.Entity<RestaurantTable>(entity =>
        {
            entity.ToTable("restaurant_tables"); entity.HasKey(x => x.Id); entity.Property(x => x.Id).HasColumnName("id"); entity.Property(x => x.RestaurantId).HasColumnName("restaurant_id"); entity.Property(x => x.Number).HasColumnName("number"); entity.Property(x => x.Seats).HasColumnName("seats"); entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(30).IsRequired(); entity.HasIndex(x => new { x.RestaurantId, x.Number }).IsUnique().HasDatabaseName("restaurant_tables_restaurant_id_number_key");
        });
        b.Entity<Customer>(entity =>
        {
            entity.ToTable("customers"); entity.HasKey(x => x.Id); entity.Property(x => x.Id).HasColumnName("id"); entity.Property(x => x.RestaurantId).HasColumnName("restaurant_id"); entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(160).IsRequired(); entity.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(30); entity.Property(x => x.Email).HasColumnName("email").HasMaxLength(180); entity.Property(x => x.BirthDate).HasColumnName("birth_date"); entity.Property(x => x.Points).HasColumnName("points"); entity.Property(x => x.CashbackBalance).HasColumnName("cashback_balance").HasPrecision(12,2); entity.Property(x => x.Active).HasColumnName("active"); entity.Property(x => x.CreatedAt).HasColumnName("created_at"); entity.HasIndex(x => new { x.RestaurantId, x.Phone }).HasDatabaseName("idx_customers_restaurant_phone");
        });
        b.Entity<LoyaltyMovement>(entity =>
        {
            entity.ToTable("loyalty_movements"); entity.HasKey(x => x.Id); entity.Property(x => x.Id).HasColumnName("id"); entity.Property(x => x.RestaurantId).HasColumnName("restaurant_id"); entity.Property(x => x.CustomerId).HasColumnName("customer_id"); entity.Property(x => x.OrderId).HasColumnName("order_id"); entity.Property(x => x.Type).HasColumnName("type").HasMaxLength(20).IsRequired(); entity.Property(x => x.Points).HasColumnName("points"); entity.Property(x => x.Cashback).HasColumnName("cashback").HasPrecision(12,2); entity.Property(x => x.Description).HasColumnName("description").HasMaxLength(240).IsRequired(); entity.Property(x => x.CreatedAt).HasColumnName("created_at"); entity.HasIndex(x => new { x.RestaurantId, x.CustomerId, x.CreatedAt }).HasDatabaseName("idx_loyalty_customer_date");
        });
        b.Entity<Coupon>(entity =>
        {
            entity.ToTable("coupons"); entity.HasKey(x => x.Id); entity.Property(x => x.Id).HasColumnName("id"); entity.Property(x => x.RestaurantId).HasColumnName("restaurant_id"); entity.Property(x => x.Code).HasColumnName("code").HasMaxLength(40).IsRequired(); entity.Property(x => x.Description).HasColumnName("description").HasMaxLength(180).IsRequired(); entity.Property(x => x.DiscountType).HasColumnName("discount_type").HasMaxLength(20).IsRequired(); entity.Property(x => x.Value).HasColumnName("value").HasPrecision(12,2); entity.Property(x => x.MinimumOrderValue).HasColumnName("minimum_order_value").HasPrecision(12,2); entity.Property(x => x.MaxUses).HasColumnName("max_uses"); entity.Property(x => x.Uses).HasColumnName("uses"); entity.Property(x => x.ExpiresAt).HasColumnName("expires_at"); entity.Property(x => x.Active).HasColumnName("active"); entity.Property(x => x.CreatedAt).HasColumnName("created_at"); entity.HasIndex(x => new { x.RestaurantId, x.Code }).IsUnique().HasDatabaseName("coupons_restaurant_code_key");
        });
        b.Entity<Order>(entity =>
        {
            entity.ToTable("orders"); entity.HasKey(x => x.Id); entity.Property(x => x.Id).HasColumnName("id"); entity.Property(x => x.RestaurantId).HasColumnName("restaurant_id"); entity.Property(x => x.TableId).HasColumnName("table_id"); entity.Property(x => x.UserId).HasColumnName("user_id"); entity.Property(x => x.CustomerId).HasColumnName("customer_id"); entity.Property(x => x.CustomerName).HasColumnName("customer_name").HasMaxLength(160); entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(30).IsRequired(); entity.Property(x => x.PaymentMethod).HasColumnName("payment_method").HasMaxLength(30); entity.Property(x => x.Total).HasColumnName("total").HasPrecision(12, 2); entity.Property(x => x.CreatedAt).HasColumnName("created_at"); entity.Property(x => x.ClosedAt).HasColumnName("closed_at"); entity.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.OrderId);
        });
        b.Entity<OrderItem>(entity =>
        {
            entity.ToTable("order_items"); entity.HasKey(x => x.Id); entity.Property(x => x.Id).HasColumnName("id"); entity.Property(x => x.OrderId).HasColumnName("order_id"); entity.Property(x => x.ProductId).HasColumnName("product_id"); entity.Property(x => x.Quantity).HasColumnName("quantity"); entity.Property(x => x.UnitPrice).HasColumnName("unit_price").HasPrecision(12, 2); entity.Property(x => x.Notes).HasColumnName("notes");
        });
        b.Entity<CashSession>(entity =>
        {
            entity.ToTable("cash_sessions"); entity.HasKey(x => x.Id); entity.Property(x => x.Id).HasColumnName("id"); entity.Property(x => x.RestaurantId).HasColumnName("restaurant_id"); entity.Property(x => x.OpenedByUserId).HasColumnName("opened_by_user_id"); entity.Property(x => x.ClosedByUserId).HasColumnName("closed_by_user_id"); entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).IsRequired(); entity.Property(x => x.OpeningAmount).HasColumnName("opening_amount").HasPrecision(12, 2); entity.Property(x => x.ExpectedCashAmount).HasColumnName("expected_cash_amount").HasPrecision(12, 2); entity.Property(x => x.CountedCashAmount).HasColumnName("counted_cash_amount").HasPrecision(12, 2); entity.Property(x => x.DifferenceAmount).HasColumnName("difference_amount").HasPrecision(12, 2); entity.Property(x => x.OpenedAt).HasColumnName("opened_at"); entity.Property(x => x.ClosedAt).HasColumnName("closed_at"); entity.HasIndex(x => new { x.RestaurantId, x.Status }).HasDatabaseName("idx_cash_sessions_restaurant_status");
        });
        b.Entity<CashMovement>(entity =>
        {
            entity.ToTable("cash_movements"); entity.HasKey(x => x.Id); entity.Property(x => x.Id).HasColumnName("id"); entity.Property(x => x.RestaurantId).HasColumnName("restaurant_id"); entity.Property(x => x.CashSessionId).HasColumnName("cash_session_id"); entity.Property(x => x.OrderId).HasColumnName("order_id"); entity.Property(x => x.Type).HasColumnName("type").HasMaxLength(20).IsRequired(); entity.Property(x => x.Description).HasColumnName("description").HasMaxLength(240).IsRequired(); entity.Property(x => x.Amount).HasColumnName("amount").HasPrecision(12, 2); entity.Property(x => x.PaymentMethod).HasColumnName("payment_method").HasMaxLength(30); entity.Property(x => x.CreatedAt).HasColumnName("created_at"); entity.HasIndex(x => new { x.RestaurantId, x.CashSessionId, x.CreatedAt }).HasDatabaseName("idx_cash_movements_session_date");
        });
        b.Entity<FinancialEntry>(entity =>
        {
            entity.ToTable("financial_entries"); entity.HasKey(x => x.Id); entity.Property(x => x.Id).HasColumnName("id"); entity.Property(x => x.RestaurantId).HasColumnName("restaurant_id"); entity.Property(x => x.Type).HasColumnName("type").HasMaxLength(20).IsRequired(); entity.Property(x => x.Category).HasColumnName("category").HasMaxLength(60).IsRequired(); entity.Property(x => x.Description).HasColumnName("description").HasMaxLength(240).IsRequired(); entity.Property(x => x.Amount).HasColumnName("amount").HasPrecision(12,2); entity.Property(x => x.DueDate).HasColumnName("due_date"); entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).IsRequired(); entity.Property(x => x.PaymentMethod).HasColumnName("payment_method").HasMaxLength(30); entity.Property(x => x.PaidAt).HasColumnName("paid_at"); entity.Property(x => x.CreatedAt).HasColumnName("created_at"); entity.HasIndex(x => new { x.RestaurantId, x.Type, x.Status, x.DueDate }).HasDatabaseName("idx_financial_entries_filter");
        });
    }
}
