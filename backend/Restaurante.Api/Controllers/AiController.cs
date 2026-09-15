using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurante.Api.Data;

namespace Restaurante.Api.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize(Roles = "ADMIN,MANAGER")]
public class AiController(AppDbContext db) : ControllerBase
{
    private Guid RestaurantId => Guid.Parse(User.FindFirst("restaurantId")!.Value);

    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] AiQuestion request)
    {
        var question = request.Question?.Trim();
        if (string.IsNullOrWhiteSpace(question))
            return BadRequest(new { message = "Digite uma pergunta." });
        if (question.Length > 500)
            return BadRequest(new { message = "A pergunta deve ter no máximo 500 caracteres." });

        var q = question.ToLowerInvariant();
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        if (q.Contains("faturamento") || q.Contains("vendas") || q.Contains("receita"))
        {
            var orders = await db.Orders.AsNoTracking()
                .Where(x => x.RestaurantId == RestaurantId && x.Status == "CLOSED" && x.ClosedAt >= today && x.ClosedAt < tomorrow)
                .Select(x => new { x.Total })
                .ToListAsync();
            var revenue = orders.Sum(x => x.Total);
            return Ok(new
            {
                answer = $"Hoje foram fechados {orders.Count} pedidos, com faturamento de {revenue:C2} e ticket médio de {(orders.Count == 0 ? 0 : revenue / orders.Count):C2}.",
                source = "database"
            });
        }

        if (q.Contains("estoque") || q.Contains("comprar") || q.Contains("repor"))
        {
            var critical = await db.Ingredients.AsNoTracking()
                .Where(x => x.RestaurantId == RestaurantId && x.CurrentQuantity <= x.MinimumQuantity)
                .OrderBy(x => x.CurrentQuantity)
                .Select(x => new { x.Name, x.CurrentQuantity, x.MinimumQuantity, x.Unit })
                .Take(20)
                .ToListAsync();

            if (critical.Count == 0)
                return Ok(new { answer = "Não há ingredientes em estoque crítico neste momento.", source = "database" });

            var items = string.Join("; ", critical.Select(x => $"{x.Name}: {x.CurrentQuantity} {x.Unit} (mínimo {x.MinimumQuantity} {x.Unit})"));
            return Ok(new { answer = $"Itens que precisam de atenção no estoque: {items}.", source = "database" });
        }

        if (q.Contains("lucro") || q.Contains("margem") || q.Contains("cmv"))
        {
            var closedOrders = await db.Orders.AsNoTracking()
                .Where(x => x.RestaurantId == RestaurantId && x.Status == "CLOSED" && x.ClosedAt >= today && x.ClosedAt < tomorrow)
                .Select(x => new { x.Id, x.Total })
                .ToListAsync();
            var ids = closedOrders.Select(x => x.Id).ToList();
            var revenue = closedOrders.Sum(x => x.Total);
            var cmv = ids.Count == 0 ? 0m : await (
                from item in db.OrderItems.AsNoTracking()
                join recipe in db.Recipes.AsNoTracking() on item.ProductId equals recipe.ProductId
                join ingredient in db.Ingredients.AsNoTracking() on recipe.IngredientId equals ingredient.Id
                where ids.Contains(item.OrderId) && ingredient.RestaurantId == RestaurantId
                select (decimal?)(item.Quantity * recipe.Quantity * ingredient.CostPerUnit)
            ).SumAsync() ?? 0m;
            var grossProfit = revenue - cmv;
            var margin = revenue == 0 ? 0 : grossProfit / revenue * 100;
            return Ok(new
            {
                answer = $"Hoje o faturamento fechado é {revenue:C2}, o CMV estimado é {cmv:C2}, o lucro bruto estimado é {grossProfit:C2} e a margem bruta estimada é {margin:N1}%.",
                source = "database"
            });
        }

        if (q.Contains("mais vendido") || q.Contains("mais vendidos") || q.Contains("produto") && q.Contains("vende"))
        {
            var top = await (
                from item in db.OrderItems.AsNoTracking()
                join order in db.Orders.AsNoTracking() on item.OrderId equals order.Id
                join product in db.Products.AsNoTracking() on item.ProductId equals product.Id
                where order.RestaurantId == RestaurantId && product.RestaurantId == RestaurantId && order.Status == "CLOSED" && order.ClosedAt >= today && order.ClosedAt < tomorrow
                group item by product.Name into g
                orderby g.Sum(x => x.Quantity) descending
                select new { Name = g.Key, Quantity = g.Sum(x => x.Quantity), Revenue = g.Sum(x => x.Quantity * x.UnitPrice) }
            ).Take(5).ToListAsync();

            if (top.Count == 0)
                return Ok(new { answer = "Ainda não há vendas fechadas hoje para montar o ranking de produtos.", source = "database" });

            var ranking = string.Join("; ", top.Select((x, i) => $"{i + 1}º {x.Name} — {x.Quantity} un. / {x.Revenue:C2}"));
            return Ok(new { answer = $"Produtos mais vendidos hoje: {ranking}.", source = "database" });
        }

        return Ok(new
        {
            answer = "Posso responder, por enquanto, sobre faturamento e vendas de hoje, estoque crítico e reposição, lucro/CMV/margem e produtos mais vendidos.",
            source = "fallback"
        });
    }
}

public record AiQuestion(string? Question);
