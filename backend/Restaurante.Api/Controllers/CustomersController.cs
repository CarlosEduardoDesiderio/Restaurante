using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurante.Api.Data;
using Restaurante.Api.Models;

namespace Restaurante.Api.Controllers;

[ApiController, Route("api/customers"), Authorize]
public class CustomersController(AppDbContext db) : ControllerBase
{
    private Guid RestaurantId => Guid.Parse(User.FindFirst("restaurantId")!.Value);
    private static DateTime? NormalizeBirthDate(DateTime? value) => value is null
        ? null
        : DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Utc);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? search)
    {
        var query = db.Customers.Where(x => x.RestaurantId == RestaurantId && x.Active);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(term)
                || (x.Phone != null && x.Phone.Contains(term))
                || (x.Email != null && x.Email.ToLower().Contains(term)));
        }

        return Ok(await query.OrderBy(x => x.Name).Take(200).ToListAsync());
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id)
    {
        var customer = await db.Customers.SingleOrDefaultAsync(x => x.Id == id && x.RestaurantId == RestaurantId);
        if (customer is null) return NotFound();

        var orders = await db.Orders
            .Where(x => x.RestaurantId == RestaurantId && x.CustomerId == id)
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .Select(x => new { x.Id, x.Total, x.Status, x.PaymentMethod, x.CreatedAt, x.ClosedAt })
            .ToListAsync();

        var loyalty = await db.LoyaltyMovements
            .Where(x => x.RestaurantId == RestaurantId && x.CustomerId == id)
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .ToListAsync();

        var closed = orders.Where(x => x.Status == "CLOSED").ToList();
        return Ok(new
        {
            customer,
            stats = new
            {
                orders = closed.Count,
                totalSpent = closed.Sum(x => x.Total),
                averageTicket = closed.Count == 0 ? 0 : closed.Sum(x => x.Total) / closed.Count,
                lastPurchase = closed.FirstOrDefault()?.ClosedAt
            },
            orders,
            loyalty
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateCustomerRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest(new { message = "Informe o nome do cliente." });
        var phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim();
        var email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim().ToLowerInvariant();

        if (phone is not null && await db.Customers.AnyAsync(x => x.RestaurantId == RestaurantId && x.Phone == phone && x.Active))
            return Conflict(new { message = "Já existe um cliente ativo com este telefone." });
        if (email is not null && await db.Customers.AnyAsync(x => x.RestaurantId == RestaurantId && x.Email == email && x.Active))
            return Conflict(new { message = "Já existe um cliente ativo com este e-mail." });

        var customer = new Customer
        {
            RestaurantId = RestaurantId,
            Name = req.Name.Trim(),
            Phone = phone,
            Email = email,
            BirthDate = NormalizeBirthDate(req.BirthDate),
            Active = true
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return Created($"/api/customers/{customer.Id}", customer);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateCustomerRequest req)
    {
        var customer = await db.Customers.SingleOrDefaultAsync(x => x.Id == id && x.RestaurantId == RestaurantId);
        if (customer is null) return NotFound();
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest(new { message = "Informe o nome do cliente." });

        customer.Name = req.Name.Trim();
        customer.Phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim();
        customer.Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim().ToLowerInvariant();
        customer.BirthDate = NormalizeBirthDate(req.BirthDate);
        customer.Active = req.Active;
        await db.SaveChangesAsync();
        return Ok(customer);
    }

    [HttpPost("{id:guid}/loyalty")]
    public async Task<IActionResult> AdjustLoyalty(Guid id, LoyaltyAdjustmentRequest req)
    {
        var customer = await db.Customers.SingleOrDefaultAsync(x => x.Id == id && x.RestaurantId == RestaurantId);
        if (customer is null) return NotFound();
        if (req.Points == 0 && req.Cashback == 0) return BadRequest(new { message = "Informe pontos ou cashback para ajustar." });

        var nextPoints = customer.Points + req.Points;
        var nextCashback = customer.CashbackBalance + req.Cashback;
        if (nextPoints < 0) return BadRequest(new { message = "O saldo de pontos não pode ficar negativo." });
        if (nextCashback < 0) return BadRequest(new { message = "O saldo de cashback não pode ficar negativo." });

        customer.Points = nextPoints;
        customer.CashbackBalance = nextCashback;
        db.LoyaltyMovements.Add(new LoyaltyMovement
        {
            RestaurantId = RestaurantId,
            CustomerId = id,
            Type = req.Points >= 0 && req.Cashback >= 0 ? "ADJUST_IN" : "ADJUST_OUT",
            Points = req.Points,
            Cashback = req.Cashback,
            Description = string.IsNullOrWhiteSpace(req.Description) ? "Ajuste manual de fidelidade" : req.Description.Trim()
        });
        await db.SaveChangesAsync();
        return Ok(customer);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        var customers = db.Customers.Where(x => x.RestaurantId == RestaurantId && x.Active);
        var total = await customers.CountAsync();
        var withPurchase = await db.Orders
            .Where(x => x.RestaurantId == RestaurantId && x.Status == "CLOSED" && x.CustomerId != null)
            .Select(x => x.CustomerId)
            .Distinct()
            .CountAsync();
        var cashback = await customers.SumAsync(x => (decimal?)x.CashbackBalance) ?? 0;
        var points = await customers.SumAsync(x => (int?)x.Points) ?? 0;
        return Ok(new { customers = total, customersWithPurchase = withPurchase, points, cashback });
    }

    [HttpGet("coupons")]
    public async Task<IActionResult> Coupons() => Ok(await db.Coupons
        .Where(x => x.RestaurantId == RestaurantId)
        .OrderByDescending(x => x.CreatedAt)
        .Take(100)
        .ToListAsync());

    [HttpPost("coupons")]
    public async Task<IActionResult> CreateCoupon(CreateCouponRequest req)
    {
        var code = req.Code.Trim().ToUpperInvariant();
        var type = req.DiscountType.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code)) return BadRequest(new { message = "Informe o código do cupom." });
        if (type is not ("PERCENT" or "FIXED")) return BadRequest(new { message = "Tipo inválido. Use PERCENT ou FIXED." });
        if (req.Value <= 0) return BadRequest(new { message = "O desconto deve ser maior que zero." });
        if (type == "PERCENT" && req.Value > 100) return BadRequest(new { message = "O desconto percentual não pode ultrapassar 100%." });
        if (await db.Coupons.AnyAsync(x => x.RestaurantId == RestaurantId && x.Code == code))
            return Conflict(new { message = "Já existe um cupom com este código." });

        var coupon = new Coupon
        {
            RestaurantId = RestaurantId,
            Code = code,
            Description = string.IsNullOrWhiteSpace(req.Description) ? code : req.Description.Trim(),
            DiscountType = type,
            Value = req.Value,
            MinimumOrderValue = Math.Max(0, req.MinimumOrderValue),
            MaxUses = req.MaxUses is > 0 ? req.MaxUses : null,
            ExpiresAt = req.ExpiresAt?.ToUniversalTime(),
            Active = true
        };
        db.Coupons.Add(coupon);
        await db.SaveChangesAsync();
        return Created($"/api/customers/coupons/{coupon.Id}", coupon);
    }

    [HttpPatch("coupons/{id:guid}/toggle")]
    public async Task<IActionResult> ToggleCoupon(Guid id)
    {
        var coupon = await db.Coupons.SingleOrDefaultAsync(x => x.Id == id && x.RestaurantId == RestaurantId);
        if (coupon is null) return NotFound();
        coupon.Active = !coupon.Active;
        await db.SaveChangesAsync();
        return Ok(coupon);
    }
}

public record CreateCustomerRequest(string Name, string? Phone, string? Email, DateTime? BirthDate);
public record UpdateCustomerRequest(string Name, string? Phone, string? Email, DateTime? BirthDate, bool Active);
public record LoyaltyAdjustmentRequest(int Points, decimal Cashback, string? Description);
public record CreateCouponRequest(string Code, string? Description, string DiscountType, decimal Value, decimal MinimumOrderValue, int? MaxUses, DateTime? ExpiresAt);
