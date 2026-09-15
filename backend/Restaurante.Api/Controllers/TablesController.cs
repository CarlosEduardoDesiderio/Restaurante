using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurante.Api.Data;
using Restaurante.Api.Models;

namespace Restaurante.Api.Controllers;

[ApiController]
[Route("api/tables")]
[Authorize]
public class TablesController(AppDbContext db) : ControllerBase
{
    private static readonly string[] AllowedStatuses = ["FREE", "OCCUPIED", "RESERVED", "DISABLED"];
    private Guid RestaurantId => Guid.Parse(User.FindFirst("restaurantId")!.Value);

    [HttpGet]
    [Authorize(Roles = "ADMIN,MANAGER,CASHIER,WAITER,KITCHEN")]
    public async Task<IActionResult> Get()
    {
        var tables = await db.Tables
            .AsNoTracking()
            .Where(x => x.RestaurantId == RestaurantId)
            .OrderBy(x => x.Number)
            .ToListAsync();
        return Ok(tables);
    }

    [HttpPost]
    [Authorize(Roles = "ADMIN,MANAGER")]
    public async Task<IActionResult> Create(RestaurantTable input)
    {
        if (input.Number <= 0)
            return BadRequest(new { message = "O número da mesa deve ser maior que zero." });
        if (input.Seats <= 0)
            return BadRequest(new { message = "A quantidade de lugares deve ser maior que zero." });
        if (await db.Tables.AnyAsync(x => x.RestaurantId == RestaurantId && x.Number == input.Number))
            return Conflict(new { message = "Já existe uma mesa com este número." });

        var table = new RestaurantTable
        {
            Id = Guid.NewGuid(),
            RestaurantId = RestaurantId,
            Number = input.Number,
            Seats = input.Seats,
            Status = "FREE"
        };

        db.Tables.Add(table);
        await db.SaveChangesAsync();
        return Created($"/api/tables/{table.Id}", table);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "ADMIN,MANAGER,CASHIER,WAITER")]
    public async Task<IActionResult> Status(Guid id, StatusRequest request)
    {
        var status = request.Status?.Trim().ToUpperInvariant();
        if (status is null || !AllowedStatuses.Contains(status))
            return BadRequest(new { message = "Status de mesa inválido." });

        var table = await db.Tables.SingleOrDefaultAsync(x => x.Id == id && x.RestaurantId == RestaurantId);
        if (table is null) return NotFound(new { message = "Mesa não encontrada." });

        table.Status = status;
        await db.SaveChangesAsync();
        return Ok(table);
    }
}

public record StatusRequest(string? Status);
