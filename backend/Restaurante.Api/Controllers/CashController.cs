using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurante.Api.Data;
using Restaurante.Api.Models;
using System.Security.Claims;

namespace Restaurante.Api.Controllers;

[ApiController, Route("api/cash"), Authorize]
public class CashController(AppDbContext db) : ControllerBase
{
    private Guid RestaurantId => Guid.Parse(User.FindFirst("restaurantId")!.Value);
    private Guid UserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    [HttpGet("current")]
    public async Task<IActionResult> Current()
    {
        var session = await db.CashSessions
            .Where(x => x.RestaurantId == RestaurantId && x.Status == "OPEN")
            .OrderByDescending(x => x.OpenedAt)
            .FirstOrDefaultAsync();

        if (session is null) return Ok(new { open = false });
        return Ok(await BuildSummary(session));
    }

    [HttpGet("history")]
    public async Task<IActionResult> History() => Ok(await db.CashSessions
        .Where(x => x.RestaurantId == RestaurantId)
        .OrderByDescending(x => x.OpenedAt)
        .Take(50)
        .ToListAsync());

    [HttpGet("{id:guid}/movements")]
    public async Task<IActionResult> Movements(Guid id)
    {
        var exists = await db.CashSessions.AnyAsync(x => x.Id == id && x.RestaurantId == RestaurantId);
        if (!exists) return NotFound();
        return Ok(await db.CashMovements
            .Where(x => x.RestaurantId == RestaurantId && x.CashSessionId == id)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync());
    }

    [HttpPost("open")]
    public async Task<IActionResult> Open(OpenCashRequest req)
    {
        if (req.OpeningAmount < 0) return BadRequest(new { message = "O valor inicial não pode ser negativo." });
        var alreadyOpen = await db.CashSessions.AnyAsync(x => x.RestaurantId == RestaurantId && x.Status == "OPEN");
        if (alreadyOpen) return Conflict(new { message = "Já existe um caixa aberto para este restaurante." });

        await using var transaction = await db.Database.BeginTransactionAsync();

        var session = new CashSession
        {
            RestaurantId = RestaurantId,
            OpenedByUserId = UserId,
            OpeningAmount = req.OpeningAmount,
            Status = "OPEN"
        };

        // Persiste a sessão antes de qualquer movimento que a referencie.
        // Isso evita violação da FK cash_movements_cash_session_id_fkey.
        db.CashSessions.Add(session);
        await db.SaveChangesAsync();

        if (req.OpeningAmount > 0)
        {
            db.CashMovements.Add(new CashMovement
            {
                RestaurantId = RestaurantId,
                CashSessionId = session.Id,
                Type = "OPENING",
                Description = "Abertura de caixa",
                Amount = req.OpeningAmount,
                PaymentMethod = "CASH"
            });
            await db.SaveChangesAsync();
        }

        await transaction.CommitAsync();
        return Ok(await BuildSummary(session));
    }

    [HttpPost("supply")]
    public async Task<IActionResult> Supply(CashOperationRequest req)
    {
        if (req.Amount <= 0) return BadRequest(new { message = "Informe um valor maior que zero." });
        var session = await GetOpenSession();
        if (session is null) return Conflict(new { message = "Abra o caixa antes de registrar um suprimento." });

        db.CashMovements.Add(new CashMovement
        {
            RestaurantId = RestaurantId,
            CashSessionId = session.Id,
            Type = "SUPPLY",
            Description = string.IsNullOrWhiteSpace(req.Description) ? "Suprimento de caixa" : req.Description.Trim(),
            Amount = req.Amount,
            PaymentMethod = "CASH"
        });
        await db.SaveChangesAsync();
        return Ok(await BuildSummary(session));
    }

    [HttpPost("withdrawal")]
    public async Task<IActionResult> Withdrawal(CashOperationRequest req)
    {
        if (req.Amount <= 0) return BadRequest(new { message = "Informe um valor maior que zero." });
        var session = await GetOpenSession();
        if (session is null) return Conflict(new { message = "Abra o caixa antes de registrar uma sangria." });

        var summary = await BuildSummaryData(session);
        if (req.Amount > summary.ExpectedCash)
            return Conflict(new { message = "A sangria não pode ser maior que o dinheiro esperado no caixa.", available = summary.ExpectedCash });

        db.CashMovements.Add(new CashMovement
        {
            RestaurantId = RestaurantId,
            CashSessionId = session.Id,
            Type = "WITHDRAWAL",
            Description = string.IsNullOrWhiteSpace(req.Description) ? "Sangria de caixa" : req.Description.Trim(),
            Amount = req.Amount,
            PaymentMethod = "CASH"
        });
        await db.SaveChangesAsync();
        return Ok(await BuildSummary(session));
    }

    [HttpPost("close")]
    public async Task<IActionResult> Close(CloseCashRequest req)
    {
        if (req.CountedCashAmount < 0) return BadRequest(new { message = "O valor contado não pode ser negativo." });
        var session = await GetOpenSession();
        if (session is null) return Conflict(new { message = "Não existe caixa aberto." });

        var data = await BuildSummaryData(session);
        session.Status = "CLOSED";
        session.ClosedByUserId = UserId;
        session.ClosedAt = DateTime.UtcNow;
        session.ExpectedCashAmount = data.ExpectedCash;
        session.CountedCashAmount = req.CountedCashAmount;
        session.DifferenceAmount = req.CountedCashAmount - data.ExpectedCash;

        await db.SaveChangesAsync();
        return Ok(new
        {
            session.Id,
            session.Status,
            session.OpenedAt,
            session.ClosedAt,
            session.OpeningAmount,
            expectedCash = data.ExpectedCash,
            countedCash = session.CountedCashAmount,
            difference = session.DifferenceAmount,
            data.Pix,
            data.Card,
            data.CashSales,
            data.TotalSales,
            data.Supplies,
            data.Withdrawals
        });
    }

    private Task<CashSession?> GetOpenSession() => db.CashSessions
        .Where(x => x.RestaurantId == RestaurantId && x.Status == "OPEN")
        .OrderByDescending(x => x.OpenedAt)
        .FirstOrDefaultAsync();

    private async Task<object> BuildSummary(CashSession session)
    {
        var data = await BuildSummaryData(session);
        return new
        {
            open = true,
            session.Id,
            session.Status,
            session.OpenedAt,
            session.OpeningAmount,
            expectedCash = data.ExpectedCash,
            data.Pix,
            data.Card,
            data.CashSales,
            data.TotalSales,
            data.Supplies,
            data.Withdrawals,
            movements = data.MovementCount
        };
    }

    private async Task<CashSummaryData> BuildSummaryData(CashSession session)
    {
        var movements = await db.CashMovements
            .Where(x => x.RestaurantId == RestaurantId && x.CashSessionId == session.Id)
            .ToListAsync();

        var sales = movements.Where(x => x.Type == "IN").ToList();
        var cashSales = sales.Where(x => x.PaymentMethod == "CASH").Sum(x => x.Amount);
        var pix = sales.Where(x => x.PaymentMethod == "PIX").Sum(x => x.Amount);
        var card = sales.Where(x => x.PaymentMethod == "CARD").Sum(x => x.Amount);
        var supplies = movements.Where(x => x.Type == "SUPPLY").Sum(x => x.Amount);
        var withdrawals = movements.Where(x => x.Type == "WITHDRAWAL").Sum(x => x.Amount);
        var expectedCash = session.OpeningAmount + cashSales + supplies - withdrawals;

        return new CashSummaryData(expectedCash, pix, card, cashSales, sales.Sum(x => x.Amount), supplies, withdrawals, movements.Count);
    }
}

public record OpenCashRequest(decimal OpeningAmount);
public record CashOperationRequest(decimal Amount, string? Description);
public record CloseCashRequest(decimal CountedCashAmount);
public record CashSummaryData(decimal ExpectedCash, decimal Pix, decimal Card, decimal CashSales, decimal TotalSales, decimal Supplies, decimal Withdrawals, int MovementCount);
