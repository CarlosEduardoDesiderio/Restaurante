using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurante.Api.Data;
using Restaurante.Api.Models;

namespace Restaurante.Api.Controllers;
[ApiController, Route("api/products"), Authorize]
public class ProductsController(AppDbContext db) : ControllerBase
{
    private Guid RestaurantId => Guid.Parse(User.FindFirst("restaurantId")!.Value);
    [HttpGet] public async Task<IActionResult> GetAll() => Ok(await db.Products.Where(x => x.RestaurantId == RestaurantId && x.Active).OrderBy(x => x.Name).ToListAsync());
    [HttpPost] public async Task<IActionResult> Create(Product product) { product.Id = Guid.NewGuid(); product.RestaurantId = RestaurantId; db.Products.Add(product); await db.SaveChangesAsync(); return Created($"/api/products/{product.Id}", product); }
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, Product input) { var p = await db.Products.SingleOrDefaultAsync(x => x.Id == id && x.RestaurantId == RestaurantId); if (p is null) return NotFound(); p.Name=input.Name; p.Description=input.Description; p.Price=input.Price; p.Active=input.Active; await db.SaveChangesAsync(); return Ok(p); }
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id) { var p=await db.Products.SingleOrDefaultAsync(x=>x.Id==id&&x.RestaurantId==RestaurantId); if(p is null)return NotFound(); p.Active=false; await db.SaveChangesAsync(); return NoContent(); }
}
