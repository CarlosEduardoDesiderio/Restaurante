namespace Restaurante.Api.Models;

public class Restaurant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Meu Restaurante";
    public string? Document { get; set; }
    public bool Active { get; set; } = true;
}

public class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RestaurantId { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "WAITER";
    public bool Active { get; set; } = true;
    public Restaurant? Restaurant { get; set; }
}

public class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RestaurantId { get; set; }
    public string Name { get; set; } = "";
    public bool Active { get; set; } = true;
}

public class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RestaurantId { get; set; }
    public Guid? CategoryId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public bool Active { get; set; } = true;
}

public class Ingredient
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RestaurantId { get; set; }
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "";
    public decimal CurrentQuantity { get; set; }
    public decimal MinimumQuantity { get; set; }
    public decimal CostPerUnit { get; set; }
    public DateTime? ExpirationDate { get; set; }
}

public class Recipe
{
    public Guid ProductId { get; set; }
    public Guid IngredientId { get; set; }
    public decimal Quantity { get; set; }
}

public class StockMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RestaurantId { get; set; }
    public Guid IngredientId { get; set; }
    public Guid? OrderId { get; set; }
    public string Type { get; set; } = "OUT";
    public decimal Quantity { get; set; }
    public string Description { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class RestaurantTable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RestaurantId { get; set; }
    public int Number { get; set; }
    public int Seats { get; set; } = 4;
    public string Status { get; set; } = "AVAILABLE";
}

public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RestaurantId { get; set; }
    public Guid? TableId { get; set; }
    public Guid? UserId { get; set; }
    public string? CustomerName { get; set; }
    public string Status { get; set; } = "OPEN";
    public string? PaymentMethod { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
    public List<OrderItem> Items { get; set; } = [];
}

public class OrderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Notes { get; set; }
}

public class CashSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RestaurantId { get; set; }
    public Guid OpenedByUserId { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public string Status { get; set; } = "OPEN";
    public decimal OpeningAmount { get; set; }
    public decimal? ExpectedCashAmount { get; set; }
    public decimal? CountedCashAmount { get; set; }
    public decimal? DifferenceAmount { get; set; }
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
}

public class CashMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RestaurantId { get; set; }
    public Guid? CashSessionId { get; set; }
    public Guid? OrderId { get; set; }
    public string Type { get; set; } = "IN";
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public string? PaymentMethod { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class FinancialEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RestaurantId { get; set; }
    public string Type { get; set; } = "PAYABLE";
    public string Category { get; set; } = "OUTROS";
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTime DueDate { get; set; }
    public string Status { get; set; } = "PENDING";
    public string? PaymentMethod { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
