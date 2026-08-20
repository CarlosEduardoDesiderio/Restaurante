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
        var revenue = await db.Orders
            .Where(x => x.RestaurantId == RestaurantId && x.CreatedAt >= today && x.CreatedAt < tomorrow && x.Status == "CLOSED")
            .SumAsync(x => (decimal?)x.Total) ?? 0;
        var orders = await db.Orders.CountAsync(x => x.RestaurantId == RestaurantId && x.CreatedAt >= today && x.CreatedAt < tomorrow);
        var critical = await db.Ingredients.CountAsync(x => x.RestaurantId == RestaurantId && x.CurrentQuantity <= x.MinimumQuantity);
        var open = await db.Orders.CountAsync(x => x.RestaurantId == RestaurantId && x.Status != "CLOSED" && x.Status != "CANCELLED");
        return Ok(new { revenue, orders, averageTicket = orders == 0 ? 0 : revenue / orders, criticalStock = critical, openOrders = open });
    }
}
