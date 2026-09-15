using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurante.Api.Data;
using Restaurante.Api.Models;

namespace Restaurante.Api.Controllers;

[ApiController]
[Route("api/products")]
[Authorize]
public class ProductsController(AppDbContext db) : ControllerBase
{
    private Guid RestaurantId => Guid.Parse(User.FindFirst("restaurantId")!.Value);

    [HttpGet]
    [Authorize(Roles = "ADMIN,MANAGER,CASHIER,WAITER,KITCHEN")]
    public async Task<IActionResult> GetAll()
    {
        var products = await db.Products
            .AsNoTracking()
            .Where(x => x.RestaurantId == RestaurantId && x.Active)
            .OrderBy(x => x.Name)
            .ToListAsync();
        return Ok(products);
    }

    [HttpPost]
    [Authorize(Roles = "ADMIN,MANAGER")]
    public async Task<IActionResult> Create(Product input)
    {
        var name = input.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new { message = "Informe o nome do produto." });
        if (input.Price < 0)
            return BadRequest(new { message = "O preço do produto não pode ser negativo." });

        var product = new Product
        {
            Id = Guid.NewGuid(),
            RestaurantId = RestaurantId,
            Name = name,
            Description = input.Description?.Trim(),
            Price = input.Price,
            Active = input.Active
        };

        db.Products.Add(product);
        await db.SaveChangesAsync();
        return Created($"/api/products/{product.Id}", product);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "ADMIN,MANAGER")]
    public async Task<IActionResult> Update(Guid id, Product input)
    {
        var product = await db.Products.SingleOrDefaultAsync(x => x.Id == id && x.RestaurantId == RestaurantId);
        if (product is null) return NotFound(new { message = "Produto não encontrado." });

        var name = input.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new { message = "Informe o nome do produto." });
        if (input.Price < 0)
            return BadRequest(new { message = "O preço do produto não pode ser negativo." });

        product.Name = name;
        product.Description = input.Description?.Trim();
        product.Price = input.Price;
        product.Active = input.Active;
        await db.SaveChangesAsync();
        return Ok(product);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "ADMIN,MANAGER")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var product = await db.Products.SingleOrDefaultAsync(x => x.Id == id && x.RestaurantId == RestaurantId);
        if (product is null) return NotFound(new { message = "Produto não encontrado." });
        product.Active = false;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
