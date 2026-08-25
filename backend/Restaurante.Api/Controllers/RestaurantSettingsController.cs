using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurante.Api.Data;

namespace Restaurante.Api.Controllers;

[ApiController]
[Route("api/restaurant-settings")]
[Authorize(Roles = "ADMIN,MANAGER")]
public class RestaurantSettingsController(AppDbContext db) : ControllerBase
{
    private Guid RestaurantId => Guid.Parse(User.FindFirst("restaurantId")!.Value);

    private Task EnsureTable() => db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS restaurant_settings (
            restaurant_id UUID PRIMARY KEY REFERENCES restaurants(id) ON DELETE CASCADE,
            phone VARCHAR(30),
            email VARCHAR(180),
            street VARCHAR(180),
            number VARCHAR(30),
            complement VARCHAR(120),
            neighborhood VARCHAR(120),
            city VARCHAR(120),
            state VARCHAR(2),
            zip_code VARCHAR(12),
            service_fee_percent NUMERIC(5,2) NOT NULL DEFAULT 0,
            opening_hours TEXT,
            logo_url VARCHAR(500),
            updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );
        """);

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        await EnsureTable();
        var restaurant = await db.Restaurants
            .Where(x => x.Id == RestaurantId && x.Active)
            .Select(x => new { x.Id, x.Name, x.Document, x.Active })
            .SingleOrDefaultAsync();

        if (restaurant is null) return NotFound(new { message = "Restaurante não encontrado." });

        var settings = await db.Database.SqlQueryRaw<RestaurantSettingsRow>("""
            SELECT phone AS "Phone", email AS "Email", street AS "Street", number AS "Number",
                   complement AS "Complement", neighborhood AS "Neighborhood", city AS "City",
                   state AS "State", zip_code AS "ZipCode", service_fee_percent AS "ServiceFeePercent",
                   opening_hours AS "OpeningHours", logo_url AS "LogoUrl", updated_at AS "UpdatedAt"
            FROM restaurant_settings
            WHERE restaurant_id = {0}
            """, RestaurantId).SingleOrDefaultAsync();

        return Ok(new
        {
            restaurant.Id,
            restaurant.Name,
            restaurant.Document,
            restaurant.Active,
            phone = settings?.Phone,
            email = settings?.Email,
            street = settings?.Street,
            number = settings?.Number,
            complement = settings?.Complement,
            neighborhood = settings?.Neighborhood,
            city = settings?.City,
            state = settings?.State,
            zipCode = settings?.ZipCode,
            serviceFeePercent = settings?.ServiceFeePercent ?? 0,
            openingHours = settings?.OpeningHours,
            logoUrl = settings?.LogoUrl,
            updatedAt = settings?.UpdatedAt
        });
    }

    [HttpPut]
    public async Task<IActionResult> Update(UpdateRestaurantSettingsRequest req)
    {
        await EnsureTable();
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { message = "Informe o nome do restaurante." });
        if (req.Name.Trim().Length > 160)
            return BadRequest(new { message = "O nome deve ter no máximo 160 caracteres." });
        if (req.ServiceFeePercent is < 0 or > 100)
            return BadRequest(new { message = "A taxa de serviço deve estar entre 0% e 100%." });
        if (!string.IsNullOrWhiteSpace(req.State) && req.State.Trim().Length != 2)
            return BadRequest(new { message = "Informe a UF com 2 caracteres." });

        var restaurant = await db.Restaurants.SingleOrDefaultAsync(x => x.Id == RestaurantId && x.Active);
        if (restaurant is null) return NotFound(new { message = "Restaurante não encontrado." });

        restaurant.Name = req.Name.Trim();
        restaurant.Document = Clean(req.Document, 30);

        await using var tx = await db.Database.BeginTransactionAsync();
        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO restaurant_settings(
                restaurant_id, phone, email, street, number, complement, neighborhood, city, state,
                zip_code, service_fee_percent, opening_hours, logo_url, updated_at)
            VALUES (
                {RestaurantId}, {Clean(req.Phone, 30)}, {NormalizeEmail(req.Email)}, {Clean(req.Street, 180)},
                {Clean(req.Number, 30)}, {Clean(req.Complement, 120)}, {Clean(req.Neighborhood, 120)},
                {Clean(req.City, 120)}, {Clean(req.State, 2)?.ToUpperInvariant()}, {Clean(req.ZipCode, 12)},
                {req.ServiceFeePercent}, {Clean(req.OpeningHours, 2000)}, {Clean(req.LogoUrl, 500)}, NOW())
            ON CONFLICT (restaurant_id) DO UPDATE SET
                phone = EXCLUDED.phone,
                email = EXCLUDED.email,
                street = EXCLUDED.street,
                number = EXCLUDED.number,
                complement = EXCLUDED.complement,
                neighborhood = EXCLUDED.neighborhood,
                city = EXCLUDED.city,
                state = EXCLUDED.state,
                zip_code = EXCLUDED.zip_code,
                service_fee_percent = EXCLUDED.service_fee_percent,
                opening_hours = EXCLUDED.opening_hours,
                logo_url = EXCLUDED.logo_url,
                updated_at = NOW()
            """);

        await tx.CommitAsync();
        return Ok(new { message = "Configurações do restaurante atualizadas com sucesso." });
    }

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string? NormalizeEmail(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
}

public record UpdateRestaurantSettingsRequest(
    string Name,
    string? Document,
    string? Phone,
    string? Email,
    string? Street,
    string? Number,
    string? Complement,
    string? Neighborhood,
    string? City,
    string? State,
    string? ZipCode,
    decimal ServiceFeePercent,
    string? OpeningHours,
    string? LogoUrl);

public class RestaurantSettingsRow
{
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Street { get; set; }
    public string? Number { get; set; }
    public string? Complement { get; set; }
    public string? Neighborhood { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public decimal ServiceFeePercent { get; set; }
    public string? OpeningHours { get; set; }
    public string? LogoUrl { get; set; }
    public DateTime UpdatedAt { get; set; }
}
