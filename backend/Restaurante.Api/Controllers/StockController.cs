using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurante.Api.Data;
using Restaurante.Api.Models;

namespace Restaurante.Api.Controllers;
[ApiController, Route("api/stock"), Authorize]
public class StockController(AppDbContext db) : ControllerBase
{
    private Guid RestaurantId => Guid.Parse(User.FindFirst("restaurantId")!.Value);
    [HttpGet] public async Task<IActionResult> Get() => Ok(await db.Ingredients.Where(x=>x.RestaurantId==RestaurantId).OrderBy(x=>x.Name).ToListAsync());
    [HttpGet("critical")] public async Task<IActionResult> Critical() => Ok(await db.Ingredients.Where(x=>x.RestaurantId==RestaurantId && x.CurrentQuantity<=x.MinimumQuantity).OrderBy(x=>x.CurrentQuantity).ToListAsync());
    [HttpPost] public async Task<IActionResult> Create(Ingredient item) { item.Id=Guid.NewGuid(); item.RestaurantId=RestaurantId; db.Ingredients.Add(item); await db.SaveChangesAsync(); return Ok(item); }
    [HttpPatch("{id:guid}/quantity")] public async Task<IActionResult> Quantity(Guid id, QuantityRequest req) { var i=await db.Ingredients.SingleOrDefaultAsync(x=>x.Id==id&&x.RestaurantId==RestaurantId); if(i is null)return NotFound(); i.CurrentQuantity=req.Quantity; await db.SaveChangesAsync(); return Ok(i); }
}
public record QuantityRequest(decimal Quantity);
