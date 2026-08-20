using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurante.Api.Data;

namespace Restaurante.Api.Controllers;

[ApiController, Route("api/dashboard"), Authorize]
public class DashboardController(AppDbContext db) : ControllerBase
{
    private Guid RestaurantId => Guid.Parse(User.FindFirst("restaurantId")!.Value);

    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var closedOrders = await db.Orders
            .Where(x => x.RestaurantId == RestaurantId && x.ClosedAt >= today && x.ClosedAt < tomorrow && x.Status == "CLOSED")
            .Select(x => new { x.Id, x.Total, x.PaymentMethod })
            .ToListAsync();

        var revenue = closedOrders.Sum(x => x.Total);
        var paidOrders = closedOrders.Count;
        var pix = closedOrders.Where(x => x.PaymentMethod == "PIX").Sum(x => x.Total);
        var card = closedOrders.Where(x => x.PaymentMethod == "CARD").Sum(x => x.Total);
        var cash = closedOrders.Where(x => x.PaymentMethod == "CASH").Sum(x => x.Total);

        var orderIds = closedOrders.Select(x => x.Id).ToList();
        var cmv = orderIds.Count == 0 ? 0m : await (
            from item in db.OrderItems
            join recipe in db.Recipes on item.ProductId equals recipe.ProductId
            join ingredient in db.Ingredients on recipe.IngredientId equals ingredient.Id
            where orderIds.Contains(item.OrderId) && ingredient.RestaurantId == RestaurantId
            select (decimal?)(item.Quantity * recipe.Quantity * ingredient.CostPerUnit)
        ).SumAsync() ?? 0m;

        var grossProfit = revenue - cmv;
        var cmvPercentage = revenue == 0 ? 0 : cmv / revenue * 100;
        var grossMarginPercentage = revenue == 0 ? 0 : grossProfit / revenue * 100;

        var ordersToday = await db.Orders.CountAsync(x => x.RestaurantId == RestaurantId && x.CreatedAt >= today && x.CreatedAt < tomorrow);
        var critical = await db.Ingredients.CountAsync(x => x.RestaurantId == RestaurantId && x.CurrentQuantity <= x.MinimumQuantity);
        var open = await db.Orders.CountAsync(x => x.RestaurantId == RestaurantId && x.Status != "CLOSED" && x.Status != "CANCELLED");

        var topProducts = orderIds.Count == 0
            ? new List<object>()
            : await (
                from item in db.OrderItems
                join product in db.Products on item.ProductId equals product.Id
                where orderIds.Contains(item.OrderId) && product.RestaurantId == RestaurantId
                group item by new { product.Id, product.Name } into g
                orderby g.Sum(x => x.Quantity) descending
                select new
                {
                    productId = g.Key.Id,
                    name = g.Key.Name,
                    quantity = g.Sum(x => x.Quantity),
                    revenue = g.Sum(x => x.Quantity * x.UnitPrice)
                }
            ).Take(5).Cast<object>().ToListAsync();

        return Ok(new
        {
            revenue,
            orders = ordersToday,
            paidOrders,
            averageTicket = paidOrders == 0 ? 0 : revenue / paidOrders,
            criticalStock = critical,
            openOrders = open,
            payments = new { pix, card, cash },
            cmv,
            cmvPercentage,
            grossProfit,
            grossMarginPercentage,
            topProducts
        });
    }
}
