using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurante.Api.Data;
using Restaurante.Api.Models;

namespace Restaurante.Api.Controllers;

[ApiController]
[Route("api/stock")]
[Authorize]
public class StockController(AppDbContext db) : ControllerBase
{
    private Guid RestaurantId => Guid.Parse(User.FindFirst("restaurantId")!.Value);

    [HttpGet]
    [Authorize(Roles = "ADMIN,MANAGER,CASHIER,KITCHEN")]
    public async Task<IActionResult> Get() => Ok(await db.Ingredients.AsNoTracking()
        .Where(x => x.RestaurantId == RestaurantId).OrderBy(x => x.Name).ToListAsync());

    [HttpGet("critical")]
    [Authorize(Roles = "ADMIN,MANAGER,KITCHEN")]
    public async Task<IActionResult> Critical() => Ok(await db.Ingredients.AsNoTracking()
        .Where(x => x.RestaurantId == RestaurantId && x.CurrentQuantity <= x.MinimumQuantity)
        .OrderBy(x => x.CurrentQuantity).ToListAsync());

    [HttpGet("movements")]
    [Authorize(Roles = "ADMIN,MANAGER")]
    public async Task<IActionResult> Movements([FromQuery] Guid? ingredientId) => Ok(await db.StockMovements.AsNoTracking()
        .Where(x => x.RestaurantId == RestaurantId && (ingredientId == null || x.IngredientId == ingredientId))
        .OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync());

    [HttpPost]
    [Authorize(Roles = "ADMIN,MANAGER")]
    public async Task<IActionResult> Create(Ingredient input)
    {
        var name = input.Name?.Trim();
        var unit = input.Unit?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest(new { message = "Informe o nome do ingrediente." });
        if (string.IsNullOrWhiteSpace(unit)) return BadRequest(new { message = "Informe a unidade do ingrediente." });
        if (input.CurrentQuantity < 0 || input.MinimumQuantity < 0 || input.CostPerUnit < 0)
            return BadRequest(new { message = "Quantidade, estoque mínimo e custo não podem ser negativos." });

        var ingredient = new Ingredient
        {
            Id = Guid.NewGuid(), RestaurantId = RestaurantId, Name = name, Unit = unit,
            CurrentQuantity = input.CurrentQuantity, MinimumQuantity = input.MinimumQuantity,
            CostPerUnit = input.CostPerUnit
        };
        db.Ingredients.Add(ingredient);
        await db.SaveChangesAsync();
        return Created($"/api/stock/{ingredient.Id}", ingredient);
    }

    [HttpPost("{id:guid}/entry")]
    [Authorize(Roles = "ADMIN,MANAGER")]
    public async Task<IActionResult> Entry(Guid id, StockEntryRequest req)
    {
        if (req.Quantity <= 0) return BadRequest(new { message = "A quantidade deve ser maior que zero." });
        var ingredient = await db.Ingredients.SingleOrDefaultAsync(x => x.Id == id && x.RestaurantId == RestaurantId);
        if (ingredient is null) return NotFound(new { message = "Ingrediente não encontrado." });
        ingredient.CurrentQuantity += req.Quantity;
        db.StockMovements.Add(new StockMovement
        {
            RestaurantId = RestaurantId, IngredientId = ingredient.Id, Type = "IN", Quantity = req.Quantity,
            Description = string.IsNullOrWhiteSpace(req.Description) ? "Entrada manual de estoque" : req.Description.Trim()
        });
        await db.SaveChangesAsync();
        return Ok(ingredient);
    }

    [HttpPatch("{id:guid}/quantity")]
    [Authorize(Roles = "ADMIN,MANAGER")]
    public async Task<IActionResult> Quantity(Guid id, QuantityRequest req)
    {
        if (req.Quantity < 0) return BadRequest(new { message = "O estoque não pode ser negativo." });
        var ingredient = await db.Ingredients.SingleOrDefaultAsync(x => x.Id == id && x.RestaurantId == RestaurantId);
        if (ingredient is null) return NotFound(new { message = "Ingrediente não encontrado." });
        var difference = req.Quantity - ingredient.CurrentQuantity;
        ingredient.CurrentQuantity = req.Quantity;
        if (difference != 0)
        {
            db.StockMovements.Add(new StockMovement
            {
                RestaurantId = RestaurantId, IngredientId = ingredient.Id, Type = "ADJUSTMENT", Quantity = difference,
                Description = "Ajuste manual de estoque"
            });
        }
        await db.SaveChangesAsync();
        return Ok(ingredient);
    }
}

public record QuantityRequest(decimal Quantity);
public record StockEntryRequest(decimal Quantity, string? Description);
