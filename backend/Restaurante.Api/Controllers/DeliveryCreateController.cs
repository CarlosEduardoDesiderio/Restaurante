using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurante.Api.Data;
using Restaurante.Api.Models;

namespace Restaurante.Api.Controllers;

[ApiController, Route("api/delivery"), Authorize]
public class DeliveryCreateController(AppDbContext db) : ControllerBase
{
    private Guid RestaurantId => Guid.Parse(User.FindFirst("restaurantId")!.Value);

    private Task EnsureTables() => db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS delivery_zones (
            id UUID PRIMARY KEY,
            restaurant_id UUID NOT NULL REFERENCES restaurants(id),
            name VARCHAR(120) NOT NULL,
            fee NUMERIC(12,2) NOT NULL DEFAULT 0,
            estimated_minutes INTEGER NOT NULL DEFAULT 45,
            active BOOLEAN NOT NULL DEFAULT TRUE,
            created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );
        CREATE UNIQUE INDEX IF NOT EXISTS delivery_zones_restaurant_name_key
            ON delivery_zones(restaurant_id, name);

        CREATE TABLE IF NOT EXISTS delivery_drivers (
            id UUID PRIMARY KEY,
            restaurant_id UUID NOT NULL REFERENCES restaurants(id),
            name VARCHAR(160) NOT NULL,
            phone VARCHAR(30),
            vehicle VARCHAR(80),
            active BOOLEAN NOT NULL DEFAULT TRUE,
            created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );

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
    """);

    [HttpPost("create")]
    public async Task<IActionResult> Create(CreateFullDeliveryRequest req)
    {
        await EnsureTables();

        if (req.Items is null || req.Items.Count == 0 || req.Items.Any(x => x.Quantity <= 0))
            return BadRequest(new { message = "Adicione pelo menos um item válido ao delivery." });
        if (string.IsNullOrWhiteSpace(req.Address) || string.IsNullOrWhiteSpace(req.Neighborhood))
            return BadRequest(new { message = "Informe endereço e bairro." });

        var zone = await db.Database.SqlQueryRaw<DeliveryZoneCreateRow>("""
            SELECT id AS "Id", fee AS "Fee", active AS "Active"
            FROM delivery_zones
            WHERE id = {0} AND restaurant_id = {1}
            """, req.ZoneId, RestaurantId).SingleOrDefaultAsync();
        if (zone is null || !zone.Active)
            return BadRequest(new { message = "Região de entrega inválida ou inativa." });

        Customer? customer = null;
        if (req.CustomerId is Guid customerId)
        {
            customer = await db.Customers.SingleOrDefaultAsync(x => x.Id == customerId && x.RestaurantId == RestaurantId && x.Active);
            if (customer is null) return BadRequest(new { message = "Cliente inválido ou inativo." });
        }

        var productIds = req.Items.Select(x => x.ProductId).Distinct().ToList();
        var products = await db.Products
            .Where(x => x.RestaurantId == RestaurantId && productIds.Contains(x.Id) && x.Active)
            .ToDictionaryAsync(x => x.Id);
        if (products.Count != productIds.Count)
            return BadRequest(new { message = "Existe produto inválido ou inativo no pedido." });

        var recipes = await db.Recipes.Where(x => productIds.Contains(x.ProductId)).ToListAsync();
        var withoutRecipe = productIds.Where(id => !recipes.Any(r => r.ProductId == id)).ToList();
        if (withoutRecipe.Count > 0)
            return Conflict(new
            {
                message = "Existem produtos sem ficha técnica cadastrada.",
                products = withoutRecipe.Select(id => products[id].Name)
            });

        var consumption = req.Items
            .SelectMany(item => recipes.Where(r => r.ProductId == item.ProductId)
                .Select(r => new { r.IngredientId, Quantity = r.Quantity * item.Quantity }))
            .GroupBy(x => x.IngredientId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        var ingredientIds = consumption.Keys.ToList();
        var ingredients = await db.Ingredients
            .Where(x => x.RestaurantId == RestaurantId && ingredientIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);

        var shortages = consumption
            .Where(x => !ingredients.ContainsKey(x.Key) || ingredients[x.Key].CurrentQuantity < x.Value)
            .Select(x => new
            {
                ingredient = ingredients.TryGetValue(x.Key, out var ingredient) ? ingredient.Name : x.Key.ToString(),
                available = ingredients.TryGetValue(x.Key, out var availableIngredient) ? availableIngredient.CurrentQuantity : 0,
                required = x.Value
            }).ToList();
        if (shortages.Count > 0)
            return Conflict(new { message = "Estoque insuficiente para criar o delivery.", shortages });

        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            var order = new Order
            {
                RestaurantId = RestaurantId,
                CustomerId = customer?.Id,
                CustomerName = customer?.Name ?? (string.IsNullOrWhiteSpace(req.CustomerName) ? null : req.CustomerName.Trim()),
                Status = "NEW",
                UserId = Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : null
            };

            foreach (var item in req.Items)
            {
                var product = products[item.ProductId];
                order.Items.Add(new OrderItem
                {
                    OrderId = order.Id,
                    ProductId = product.Id,
                    Quantity = item.Quantity,
                    UnitPrice = product.Price,
                    Notes = string.IsNullOrWhiteSpace(item.Notes) ? null : item.Notes.Trim()
                });
                order.Total += product.Price * item.Quantity;
            }

            order.Total += zone.Fee;

            foreach (var used in consumption)
            {
                ingredients[used.Key].CurrentQuantity -= used.Value;
                db.StockMovements.Add(new StockMovement
                {
                    RestaurantId = RestaurantId,
                    IngredientId = used.Key,
                    OrderId = order.Id,
                    Type = "OUT",
                    Quantity = used.Value,
                    Description = $"Consumo do delivery {order.Id}"
                });
            }

            db.Orders.Add(order);
            await db.SaveChangesAsync();

            var deliveryId = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO delivery_orders(
                    id, restaurant_id, order_id, zone_id, address, number,
                    complement, neighborhood, reference, delivery_fee, status)
                VALUES (
                    {deliveryId}, {RestaurantId}, {order.Id}, {req.ZoneId}, {req.Address.Trim()}, {req.Number},
                    {req.Complement}, {req.Neighborhood.Trim()}, {req.Reference}, {zone.Fee}, 'WAITING')
                """);

            await transaction.CommitAsync();
            return Created($"/api/delivery/orders/{deliveryId}", new
            {
                id = deliveryId,
                orderId = order.Id,
                deliveryFee = zone.Fee,
                total = order.Total
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "Não foi possível criar o delivery.", detail = ex.InnerException?.Message ?? ex.Message });
        }
    }
}

public record CreateFullDeliveryRequest(
    Guid? CustomerId,
    string? CustomerName,
    Guid ZoneId,
    string Address,
    string? Number,
    string? Complement,
    string Neighborhood,
    string? Reference,
    List<CreateFullDeliveryItem> Items);

public record CreateFullDeliveryItem(Guid ProductId, int Quantity, string? Notes);
public class DeliveryZoneCreateRow
{
    public Guid Id { get; set; }
    public decimal Fee { get; set; }
    public bool Active { get; set; }
}
