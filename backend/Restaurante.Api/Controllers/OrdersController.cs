using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurante.Api.Data;
using Restaurante.Api.Models;

namespace Restaurante.Api.Controllers;

[ApiController, Route("api/orders"), Authorize]
public class OrdersController(AppDbContext db) : ControllerBase
{
    private Guid RestaurantId => Guid.Parse(User.FindFirst("restaurantId")!.Value);

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? status) => Ok(await db.Orders
        .Include(x => x.Items)
        .Where(x => x.RestaurantId == RestaurantId && (status == null || x.Status == status))
        .OrderByDescending(x => x.CreatedAt)
        .Take(100)
        .ToListAsync());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var order = await db.Orders.Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == id && x.RestaurantId == RestaurantId);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateOrderRequest req)
    {
        if (req.Items.Count == 0 || req.Items.Any(x => x.Quantity <= 0))
            return BadRequest(new { message = "Itens inválidos." });

        if (req.TableId is Guid tableId)
        {
            var tableExists = await db.Tables.AnyAsync(x => x.Id == tableId && x.RestaurantId == RestaurantId);
            if (!tableExists) return BadRequest(new { message = "Mesa inválida para este restaurante." });
        }

        var ids = req.Items.Select(x => x.ProductId).Distinct().ToList();
        var products = await db.Products
            .Where(x => x.RestaurantId == RestaurantId && ids.Contains(x.Id) && x.Active)
            .ToDictionaryAsync(x => x.Id);
        if (products.Count != ids.Count) return BadRequest(new { message = "Produto inválido ou inativo." });

        var recipes = await db.Recipes.Where(x => ids.Contains(x.ProductId)).ToListAsync();
        var withoutRecipe = ids.Where(id => !recipes.Any(r => r.ProductId == id)).ToList();
        if (withoutRecipe.Count > 0)
            return Conflict(new
            {
                message = "Existem produtos sem ficha técnica cadastrada.",
                products = withoutRecipe.Select(id => new { id, name = products[id].Name })
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
        if (ingredients.Count != ingredientIds.Count)
            return Conflict(new { message = "A ficha técnica contém ingredientes indisponíveis para este restaurante." });

        var shortages = consumption
            .Where(x => ingredients[x.Key].CurrentQuantity < x.Value)
            .Select(x => new
            {
                ingredientId = x.Key,
                ingredients[x.Key].Name,
                available = ingredients[x.Key].CurrentQuantity,
                required = x.Value,
                ingredients[x.Key].Unit
            }).ToList();
        if (shortages.Count > 0)
            return Conflict(new { message = "Estoque insuficiente para concluir o pedido.", shortages });

        await using var transaction = await db.Database.BeginTransactionAsync();
        var order = new Order
        {
            RestaurantId = RestaurantId,
            TableId = req.TableId,
            CustomerName = string.IsNullOrWhiteSpace(req.CustomerName) ? null : req.CustomerName.Trim(),
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
                Description = $"Consumo do pedido {order.Id}"
            });
        }

        db.Orders.Add(order);
        if (order.TableId is Guid tid)
        {
            var table = await db.Tables.SingleAsync(x => x.Id == tid && x.RestaurantId == RestaurantId);
            table.Status = "OCCUPIED";
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Created($"/api/orders/{order.Id}", order);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateOrderStatusRequest req)
    {
        var order = await db.Orders.SingleOrDefaultAsync(x => x.Id == id && x.RestaurantId == RestaurantId);
        if (order is null) return NotFound();
        if (order.Status is "CLOSED" or "CANCELLED")
            return BadRequest(new { message = "Pedido finalizado não pode mudar de status." });

        var requested = req.Status.Trim().ToUpperInvariant();
        var current = order.Status == "OPEN" ? "NEW" : order.Status;
        var allowedNext = current switch
        {
            "NEW" => "PREPARING",
            "PREPARING" => "READY",
            "READY" => "DELIVERED",
            _ => null
        };

        if (allowedNext is null || requested != allowedNext)
            return BadRequest(new { message = $"Transição inválida. Status atual: {current}. Próximo status permitido: {allowedNext ?? "nenhum"}." });

        order.Status = requested;
        await db.SaveChangesAsync();
        return Ok(order);
    }

    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id, CloseOrderRequest req)
    {
        var order = await db.Orders.Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == id && x.RestaurantId == RestaurantId);
        if (order is null) return NotFound();
        if (order.Status == "CLOSED") return BadRequest(new { message = "Pedido já fechado." });
        if (order.Status == "CANCELLED") return BadRequest(new { message = "Pedido cancelado não pode ser fechado." });
        if (order.Status != "DELIVERED") return BadRequest(new { message = "O pedido precisa estar ENTREGUE antes do fechamento." });

        var paymentMethod = req.PaymentMethod.Trim().ToUpperInvariant();
        var allowedPaymentMethods = new[] { "PIX", "CARD", "CASH" };
        if (!allowedPaymentMethods.Contains(paymentMethod))
            return BadRequest(new { message = "Forma de pagamento inválida. Use PIX, CARD ou CASH." });

        var cashSession = await db.CashSessions
            .Where(x => x.RestaurantId == RestaurantId && x.Status == "OPEN")
            .OrderByDescending(x => x.OpenedAt)
            .FirstOrDefaultAsync();
        if (cashSession is null)
            return Conflict(new { message = "Abra o caixa antes de receber pagamentos." });

        await using var transaction = await db.Database.BeginTransactionAsync();
        order.Status = "CLOSED";
        order.PaymentMethod = paymentMethod;
        order.ClosedAt = DateTime.UtcNow;
        db.CashMovements.Add(new CashMovement
        {
            RestaurantId = RestaurantId,
            CashSessionId = cashSession.Id,
            OrderId = order.Id,
            Type = "IN",
            Description = $"Pagamento do pedido {order.Id}",
            Amount = order.Total,
            PaymentMethod = paymentMethod
        });

        if (order.TableId is Guid tid)
        {
            var table = await db.Tables.SingleOrDefaultAsync(x => x.Id == tid && x.RestaurantId == RestaurantId);
            if (table != null) table.Status = "AVAILABLE";
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok(order);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var order = await db.Orders.SingleOrDefaultAsync(x => x.Id == id && x.RestaurantId == RestaurantId);
        if (order is null) return NotFound();
        if (order.Status == "CLOSED") return BadRequest(new { message = "Pedido fechado não pode ser cancelado." });
        if (order.Status == "CANCELLED") return BadRequest(new { message = "Pedido já cancelado." });

        await using var transaction = await db.Database.BeginTransactionAsync();
        var restoreStock = order.Status is "NEW" or "OPEN";
        if (restoreStock)
        {
            var movements = await db.StockMovements
                .Where(x => x.RestaurantId == RestaurantId && x.OrderId == id && x.Type == "OUT")
                .ToListAsync();
            var ingredientIds = movements.Select(x => x.IngredientId).Distinct().ToList();
            var ingredients = await db.Ingredients
                .Where(x => x.RestaurantId == RestaurantId && ingredientIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id);

            foreach (var movement in movements)
            {
                if (!ingredients.TryGetValue(movement.IngredientId, out var ingredient)) continue;
                ingredient.CurrentQuantity += movement.Quantity;
                db.StockMovements.Add(new StockMovement
                {
                    RestaurantId = RestaurantId,
                    IngredientId = movement.IngredientId,
                    OrderId = order.Id,
                    Type = "RETURN",
                    Quantity = movement.Quantity,
                    Description = $"Estorno do pedido {order.Id} antes do preparo"
                });
            }
        }

        order.Status = "CANCELLED";
        if (order.TableId is Guid tid)
        {
            var table = await db.Tables.SingleOrDefaultAsync(x => x.Id == tid && x.RestaurantId == RestaurantId);
            if (table != null) table.Status = "AVAILABLE";
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(new
        {
            order.Id,
            order.Status,
            stockRestored = restoreStock,
            message = restoreStock
                ? "Pedido cancelado e ingredientes devolvidos ao estoque."
                : "Pedido cancelado sem estorno de estoque, pois o preparo já havia iniciado."
        });
    }
}

public record CreateOrderRequest(Guid? TableId, string? CustomerName, List<CreateOrderItem> Items);
public record CreateOrderItem(Guid ProductId, int Quantity, string? Notes);
public record UpdateOrderStatusRequest(string Status);
public record CloseOrderRequest(string PaymentMethod);
