using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurante.Api.Data;
using Restaurante.Api.Models;

namespace Restaurante.Api.Controllers;

[ApiController, Route("api/financial"), Authorize]
public class FinancialController(AppDbContext db) : ControllerBase
{
    private Guid RestaurantId => Guid.Parse(User.FindFirst("restaurantId")!.Value);

    private Task EnsureTable() => db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS financial_entries (
            id UUID PRIMARY KEY,
            restaurant_id UUID NOT NULL REFERENCES restaurants(id),
            type VARCHAR(20) NOT NULL,
            category VARCHAR(60) NOT NULL,
            description VARCHAR(240) NOT NULL,
            amount NUMERIC(12,2) NOT NULL,
            due_date TIMESTAMPTZ NOT NULL,
            status VARCHAR(20) NOT NULL DEFAULT 'PENDING',
            payment_method VARCHAR(30),
            paid_at TIMESTAMPTZ,
            created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );
        CREATE INDEX IF NOT EXISTS idx_financial_entries_filter
            ON financial_entries(restaurant_id, type, status, due_date);
        """);

    [HttpGet("entries")]
    public async Task<IActionResult> Entries([FromQuery] string? type, [FromQuery] string? status)
    {
        await EnsureTable();
        var query = db.FinancialEntries.Where(x => x.RestaurantId == RestaurantId);
        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(x => x.Type == type.ToUpper());
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status.ToUpper());
        return Ok(await query.OrderBy(x => x.Status).ThenBy(x => x.DueDate).Take(200).ToListAsync());
    }

    [HttpPost("entries")]
    public async Task<IActionResult> Create(CreateFinancialEntryRequest req)
    {
        await EnsureTable();
        var type = req.Type.Trim().ToUpperInvariant();
        if (type is not ("PAYABLE" or "RECEIVABLE")) return BadRequest(new { message = "Tipo inválido. Use PAYABLE ou RECEIVABLE." });
        if (req.Amount <= 0) return BadRequest(new { message = "O valor deve ser maior que zero." });
        if (string.IsNullOrWhiteSpace(req.Description)) return BadRequest(new { message = "Informe uma descrição." });

        var entry = new FinancialEntry
        {
            RestaurantId = RestaurantId,
            Type = type,
            Category = string.IsNullOrWhiteSpace(req.Category) ? "OUTROS" : req.Category.Trim().ToUpperInvariant(),
            Description = req.Description.Trim(),
            Amount = req.Amount,
            DueDate = req.DueDate.ToUniversalTime(),
            Status = "PENDING"
        };
        db.FinancialEntries.Add(entry);
        await db.SaveChangesAsync();
        return Created($"/api/financial/entries/{entry.Id}", entry);
    }

    [HttpPatch("entries/{id:guid}/pay")]
    public async Task<IActionResult> Pay(Guid id, PayFinancialEntryRequest req)
    {
        await EnsureTable();
        var entry = await db.FinancialEntries.SingleOrDefaultAsync(x => x.Id == id && x.RestaurantId == RestaurantId);
        if (entry is null) return NotFound();
        if (entry.Status == "PAID") return BadRequest(new { message = "Lançamento já liquidado." });
        entry.Status = "PAID";
        entry.PaidAt = DateTime.UtcNow;
        entry.PaymentMethod = string.IsNullOrWhiteSpace(req.PaymentMethod) ? null : req.PaymentMethod.Trim().ToUpperInvariant();
        await db.SaveChangesAsync();
        return Ok(entry);
    }

    [HttpDelete("entries/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await EnsureTable();
        var entry = await db.FinancialEntries.SingleOrDefaultAsync(x => x.Id == id && x.RestaurantId == RestaurantId);
        if (entry is null) return NotFound();
        db.FinancialEntries.Remove(entry);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        await EnsureTable();
        var start = (from ?? DateTime.UtcNow.Date).ToUniversalTime();
        var endExclusive = (to?.Date.AddDays(1) ?? DateTime.UtcNow.Date.AddDays(1)).ToUniversalTime();
        if (endExclusive <= start) return BadRequest(new { message = "Período inválido." });

        var orders = await db.Orders
            .Where(x => x.RestaurantId == RestaurantId && x.Status == "CLOSED" && x.ClosedAt >= start && x.ClosedAt < endExclusive)
            .Select(x => new { x.Id, x.Total, x.PaymentMethod, x.ClosedAt })
            .ToListAsync();
        var ids = orders.Select(x => x.Id).ToList();
        var revenue = orders.Sum(x => x.Total);
        var cmv = ids.Count == 0 ? 0m : await (
            from item in db.OrderItems
            join recipe in db.Recipes on item.ProductId equals recipe.ProductId
            join ingredient in db.Ingredients on recipe.IngredientId equals ingredient.Id
            where ids.Contains(item.OrderId) && ingredient.RestaurantId == RestaurantId
            select (decimal?)(item.Quantity * recipe.Quantity * ingredient.CostPerUnit)
        ).SumAsync() ?? 0m;

        var entries = await db.FinancialEntries
            .Where(x => x.RestaurantId == RestaurantId && x.DueDate >= start && x.DueDate < endExclusive)
            .ToListAsync();
        var paidExpenses = entries.Where(x => x.Type == "PAYABLE" && x.Status == "PAID").Sum(x => x.Amount);
        var pendingPayables = entries.Where(x => x.Type == "PAYABLE" && x.Status == "PENDING").Sum(x => x.Amount);
        var paidReceivables = entries.Where(x => x.Type == "RECEIVABLE" && x.Status == "PAID").Sum(x => x.Amount);
        var pendingReceivables = entries.Where(x => x.Type == "RECEIVABLE" && x.Status == "PENDING").Sum(x => x.Amount);
        var grossProfit = revenue - cmv;
        var netProfit = grossProfit - paidExpenses;

        var daily = orders.GroupBy(x => x.ClosedAt!.Value.Date)
            .OrderBy(x => x.Key)
            .Select(x => new { date = x.Key, revenue = x.Sum(y => y.Total), orders = x.Count() })
            .ToList();

        return Ok(new
        {
            from = start,
            to = endExclusive.AddTicks(-1),
            revenue,
            orders = orders.Count,
            averageTicket = orders.Count == 0 ? 0 : revenue / orders.Count,
            payments = new
            {
                pix = orders.Where(x => x.PaymentMethod == "PIX").Sum(x => x.Total),
                card = orders.Where(x => x.PaymentMethod == "CARD").Sum(x => x.Total),
                cash = orders.Where(x => x.PaymentMethod == "CASH").Sum(x => x.Total)
            },
            cmv,
            cmvPercentage = revenue == 0 ? 0 : cmv / revenue * 100,
            grossProfit,
            grossMarginPercentage = revenue == 0 ? 0 : grossProfit / revenue * 100,
            paidExpenses,
            pendingPayables,
            paidReceivables,
            pendingReceivables,
            netProfit,
            netMarginPercentage = revenue == 0 ? 0 : netProfit / revenue * 100,
            daily
        });
    }
}

public record CreateFinancialEntryRequest(string Type, string Category, string Description, decimal Amount, DateTime DueDate);
public record PayFinancialEntryRequest(string? PaymentMethod);
