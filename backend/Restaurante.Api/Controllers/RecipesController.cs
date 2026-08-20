using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurante.Api.Data;
using Restaurante.Api.Models;

namespace Restaurante.Api.Controllers;

[ApiController, Route("api/recipes"), Authorize]
public class RecipesController(AppDbContext db) : ControllerBase
{
    private Guid RestaurantId => Guid.Parse(User.FindFirst("restaurantId")!.Value);

    [HttpGet("{productId:guid}")]
    public async Task<IActionResult> Get(Guid productId)
    {
        var product = await db.Products.SingleOrDefaultAsync(x => x.Id == productId && x.RestaurantId == RestaurantId);
        if (product is null) return NotFound();

        var recipe = await (from r in db.Recipes
                            join i in db.Ingredients on r.IngredientId equals i.Id
                            where r.ProductId == productId && i.RestaurantId == RestaurantId
                            orderby i.Name
                            select new { r.IngredientId, i.Name, i.Unit, r.Quantity, i.CostPerUnit })
            .ToListAsync();

        return Ok(new
        {
            product.Id,
            product.Name,
            product.Price,
            Ingredients = recipe,
            Cost = recipe.Sum(x => x.Quantity * x.CostPerUnit)
        });
    }

    [HttpPut("{productId:guid}")]
    public async Task<IActionResult> Save(Guid productId, SaveRecipeRequest request)
    {
        var productExists = await db.Products.AnyAsync(x => x.Id == productId && x.RestaurantId == RestaurantId);
        if (!productExists) return NotFound();
        if (request.Items.Any(x => x.Quantity <= 0)) return BadRequest(new { message = "A quantidade deve ser maior que zero." });

        var ingredientIds = request.Items.Select(x => x.IngredientId).Distinct().ToList();
        if (ingredientIds.Count != request.Items.Count)
            return BadRequest(new { message = "Não repita ingredientes na ficha técnica." });

        var validCount = await db.Ingredients.CountAsync(x => x.RestaurantId == RestaurantId && ingredientIds.Contains(x.Id));
        if (validCount != ingredientIds.Count)
            return BadRequest(new { message = "A ficha técnica contém ingrediente inválido." });

        var old = await db.Recipes.Where(x => x.ProductId == productId).ToListAsync();
        db.Recipes.RemoveRange(old);
        db.Recipes.AddRange(request.Items.Select(x => new Recipe
        {
            ProductId = productId,
            IngredientId = x.IngredientId,
            Quantity = x.Quantity
        }));
        await db.SaveChangesAsync();
        return NoContent();
    }
}

public record SaveRecipeRequest(List<SaveRecipeItem> Items);
public record SaveRecipeItem(Guid IngredientId, decimal Quantity);
