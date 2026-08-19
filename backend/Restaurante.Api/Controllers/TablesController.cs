using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurante.Api.Data;
using Restaurante.Api.Models;
namespace Restaurante.Api.Controllers;
[ApiController,Route("api/tables"),Authorize]
public class TablesController(AppDbContext db):ControllerBase
{
 private Guid RestaurantId=>Guid.Parse(User.FindFirst("restaurantId")!.Value);
 [HttpGet] public async Task<IActionResult> Get()=>Ok(await db.Tables.Where(x=>x.RestaurantId==RestaurantId).OrderBy(x=>x.Number).ToListAsync());
 [HttpPost] public async Task<IActionResult> Create(RestaurantTable t){t.Id=Guid.NewGuid();t.RestaurantId=RestaurantId;db.Tables.Add(t);await db.SaveChangesAsync();return Ok(t);}
 [HttpPatch("{id:guid}/status")] public async Task<IActionResult> Status(Guid id, StatusRequest r){var t=await db.Tables.SingleOrDefaultAsync(x=>x.Id==id&&x.RestaurantId==RestaurantId);if(t is null)return NotFound();t.Status=r.Status.ToUpperInvariant();await db.SaveChangesAsync();return Ok(t);}
}
public record StatusRequest(string Status);
