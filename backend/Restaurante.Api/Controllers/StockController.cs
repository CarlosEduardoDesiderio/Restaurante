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

    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await db.Ingredients.Where(x => x.RestaurantId == RestaurantId).OrderBy(x => x.Name).ToListAsync());

    [HttpGet("critical")]
    public async Task<IActionResult> Critical() => Ok(await db.Ingredients.Where(x => x.RestaurantId == RestaurantId && x.CurrentQuantity <= x.MinimumQuantity).OrderBy(x => x.CurrentQuantity).ToListAsync());

    [HttpGet("movements")]
    public async Task<IActionResult> Movements([FromQuery] Guid? ingredientId) => Ok(await db.StockMovements
        .Where(x => x.RestaurantId == RestaurantId && (ingredientId == null || x.IngredientId == ingredientId))
        .OrderByDescending(x => x.CreatedAt)
        .Take(200)
        .ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create(Ingredient item)
    {
        item.Id = Guid.NewGuid();
        item.RestaurantId = RestaurantId;
        db.Ingredients.Add(item);
        await db.SaveChangesAsync();
        return Ok(item);
    }

    [HttpPost("{id:guid}/entry")]
    public async Task<IActionResult> Entry(Guid id, StockEntryRequest req)
    {
        if (req.Quantity <= 0) return BadRequest(new { message = "A quantidade deve ser maior que zero." });
        var ingredient = await db.Ingredients.SingleOrDefaultAsync(x => x.Id == id && x.RestaurantId == RestaurantId);
        if (ingredient is null) return NotFound();
        ingredient.CurrentQuantity += req.Quantity;
        db.StockMovements.Add(new StockMovement
        {
            RestaurantId = RestaurantId,
            IngredientId = ingredient.Id,
            Type = "IN",
            Quantity = req.Quantity,
            Description = string.IsNullOrWhiteSpace(req.Description) ? "Entrada manual de estoque" : req.Description
        });
        await db.SaveChangesAsync();
        return Ok(ingredient);
    }

    [HttpPatch("{id:guid}/quantity")]
    public async Task<IActionResult> Quantity(Guid id, QuantityRequest req)
    {
        if (req.Quantity < 0) return BadRequest(new { message = "O estoque não pode ser negativo." });
        var ingredient = await db.Ingredients.SingleOrDefaultAsync(x => x.Id == id && x.RestaurantId == RestaurantId);
        if (ingredient is null) return NotFound();
        var difference = req.Quantity - ingredient.CurrentQuantity;
        ingredient.CurrentQuantity = req.Quantity;
        if (difference != 0)
        {
            db.StockMovements.Add(new StockMovement
            {
                RestaurantId = RestaurantId,
                IngredientId = ingredient.Id,
                Type = "ADJUSTMENT",
                Quantity = difference,
                Description = "Ajuste manual de estoque"
            });
        }
        await db.SaveChangesAsync();
        return Ok(ingredient);
    }
}

public record QuantityRequest(decimal Quantity);
public record StockEntryRequest(decimal Quantity, string? Description);
