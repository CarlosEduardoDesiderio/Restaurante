using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurante.Api.Data;
using Restaurante.Api.Models;
using System.Net.Mail;

namespace Restaurante.Api.Controllers;

[ApiController, Route("api/customers"), Authorize]
public class CustomersController(AppDbContext db) : ControllerBase
{
    private Guid RestaurantId => Guid.Parse(User.FindFirst("restaurantId")!.Value);
    private static DateTime? NormalizeBirthDate(DateTime? value) => value is null
        ? null
        : DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Utc);

    [HttpGet]
    [Authorize(Roles = "ADMIN,MANAGER,CASHIER,WAITER")]
    public async Task<IActionResult> List([FromQuery] string? search)
    {
        var query = db.Customers.AsNoTracking().Where(x => x.RestaurantId == RestaurantId && x.Active);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Name.ToLower().Contains(term)
                || (x.Phone != null && x.Phone.Contains(term))
                || (x.Email != null && x.Email.ToLower().Contains(term)));
        }

        return Ok(await query.OrderBy(x => x.Name).Take(200).ToListAsync());
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "ADMIN,MANAGER,CASHIER,WAITER")]
    public async Task<IActionResult> Detail(Guid id)
    {
        var customer = await db.Customers.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id && x.RestaurantId == RestaurantId);
        if (customer is null) return NotFound();

        var orders = await db.Orders.AsNoTracking()
            .Where(x => x.RestaurantId == RestaurantId && x.CustomerId == id)
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .Select(x => new { x.Id, x.Total, x.Status, x.PaymentMethod, x.CreatedAt, x.ClosedAt })
            .ToListAsync();

        var loyalty = await db.LoyaltyMovements.AsNoTracking()
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
    [Authorize(Roles = "ADMIN,MANAGER,CASHIER,WAITER")]
    public async Task<IActionResult> Create(CreateCustomerRequest req)
    {
        var validation = ValidateCustomer(req.Name, req.Phone, req.Email, req.BirthDate);
        if (validation is not null) return BadRequest(new { message = validation });

        var phone = NormalizePhone(req.Phone);
        var email = NormalizeEmail(req.Email);

        if (phone is not null && await db.Customers.AnyAsync(x => x.RestaurantId == RestaurantId && x.Phone == phone && x.Active))
            return Conflict(new { message = "Já existe um cliente ativo com este telefone." });
        if (email is not null && await db.Customers.AnyAsync(x => x.RestaurantId == RestaurantId && x.Email == email && x.Active))
            return Conflict(new { message = "Já existe um cliente ativo com este e-mail." });

        var customer = new Customer
        {
            RestaurantId = RestaurantId,
            Name = req.Name!.Trim(),
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
    [Authorize(Roles = "ADMIN,MANAGER,CASHIER")]
    public async Task<IActionResult> Update(Guid id, UpdateCustomerRequest req)
    {
        var customer = await db.Customers.SingleOrDefaultAsync(x => x.Id == id && x.RestaurantId == RestaurantId);
        if (customer is null) return NotFound();

        var validation = ValidateCustomer(req.Name, req.Phone, req.Email, req.BirthDate);
        if (validation is not null) return BadRequest(new { message = validation });

        var phone = NormalizePhone(req.Phone);
        var email = NormalizeEmail(req.Email);
        if (phone is not null && await db.Customers.AnyAsync(x => x.RestaurantId == RestaurantId && x.Id != id && x.Phone == phone && x.Active))
            return Conflict(new { message = "Já existe outro cliente ativo com este telefone." });
        if (email is not null && await db.Customers.AnyAsync(x => x.RestaurantId == RestaurantId && x.Id != id && x.Email == email && x.Active))
            return Conflict(new { message = "Já existe outro cliente ativo com este e-mail." });

        customer.Name = req.Name!.Trim();
        customer.Phone = phone;
        customer.Email = email;
        customer.BirthDate = NormalizeBirthDate(req.BirthDate);
        customer.Active = req.Active;
        await db.SaveChangesAsync();
        return Ok(customer);
    }

    [HttpPost("{id:guid}/loyalty")]
    [Authorize(Roles = "ADMIN,MANAGER")]
    public async Task<IActionResult> AdjustLoyalty(Guid id, LoyaltyAdjustmentRequest req)
    {
        var customer = await db.Customers.SingleOrDefaultAsync(x => x.Id == id && x.RestaurantId == RestaurantId && x.Active);
        if (customer is null) return NotFound();
        if (req.Points == 0 && req.Cashback == 0) return BadRequest(new { message = "Informe pontos ou cashback para ajustar." });
        if (Math.Abs(req.Points) > 100000 || Math.Abs(req.Cashback) > 100000m)
            return BadRequest(new { message = "O ajuste informado ultrapassa o limite permitido." });

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
            Description = string.IsNullOrWhiteSpace(req.Description) ? "Ajuste manual de fidelidade" : req.Description.Trim()[..Math.Min(req.Description.Trim().Length, 250)]
        });
        await db.SaveChangesAsync();
        return Ok(customer);
    }

    [HttpGet("summary")]
    [Authorize(Roles = "ADMIN,MANAGER")]
    public async Task<IActionResult> Summary()
    {
        var customers = db.Customers.AsNoTracking().Where(x => x.RestaurantId == RestaurantId && x.Active);
        var total = await customers.CountAsync();
        var withPurchase = await db.Orders.AsNoTracking()
            .Where(x => x.RestaurantId == RestaurantId && x.Status == "CLOSED" && x.CustomerId != null)
            .Select(x => x.CustomerId)
            .Distinct()
            .CountAsync();
        var cashback = await customers.SumAsync(x => (decimal?)x.CashbackBalance) ?? 0;
        var points = await customers.SumAsync(x => (int?)x.Points) ?? 0;
        return Ok(new { customers = total, customersWithPurchase = withPurchase, points, cashback });
    }

    [HttpGet("coupons")]
    [Authorize(Roles = "ADMIN,MANAGER,CASHIER")]
    public async Task<IActionResult> Coupons() => Ok(await db.Coupons.AsNoTracking()
        .Where(x => x.RestaurantId == RestaurantId)
        .OrderByDescending(x => x.CreatedAt)
        .Take(100)
        .ToListAsync());

    [HttpPost("coupons")]
    [Authorize(Roles = "ADMIN,MANAGER")]
    public async Task<IActionResult> CreateCoupon(CreateCouponRequest req)
    {
        var code = req.Code?.Trim().ToUpperInvariant();
        var type = req.DiscountType?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code)) return BadRequest(new { message = "Informe o código do cupom." });
        if (code.Length > 40) return BadRequest(new { message = "O código do cupom deve ter no máximo 40 caracteres." });
        if (type is not ("PERCENT" or "FIXED")) return BadRequest(new { message = "Tipo inválido. Use PERCENT ou FIXED." });
        if (req.Value <= 0) return BadRequest(new { message = "O desconto deve ser maior que zero." });
        if (type == "PERCENT" && req.Value > 100) return BadRequest(new { message = "O desconto percentual não pode ultrapassar 100%." });
        if (req.MinimumOrderValue < 0) return BadRequest(new { message = "O pedido mínimo não pode ser negativo." });
        if (req.MaxUses is <= 0) return BadRequest(new { message = "O limite de usos deve ser maior que zero quando informado." });
        if (req.ExpiresAt is DateTime expiresAt && expiresAt.ToUniversalTime() <= DateTime.UtcNow)
            return BadRequest(new { message = "A validade do cupom deve estar no futuro." });
        if (await db.Coupons.AnyAsync(x => x.RestaurantId == RestaurantId && x.Code == code))
            return Conflict(new { message = "Já existe um cupom com este código." });

        var coupon = new Coupon
        {
            RestaurantId = RestaurantId,
            Code = code,
            Description = string.IsNullOrWhiteSpace(req.Description) ? code : req.Description.Trim(),
            DiscountType = type,
            Value = req.Value,
            MinimumOrderValue = req.MinimumOrderValue,
            MaxUses = req.MaxUses,
            ExpiresAt = req.ExpiresAt?.ToUniversalTime(),
            Active = true
        };
        db.Coupons.Add(coupon);
        await db.SaveChangesAsync();
        return Created($"/api/customers/coupons/{coupon.Id}", coupon);
    }

    [HttpPatch("coupons/{id:guid}/toggle")]
    [Authorize(Roles = "ADMIN,MANAGER")]
    public async Task<IActionResult> ToggleCoupon(Guid id)
    {
        var coupon = await db.Coupons.SingleOrDefaultAsync(x => x.Id == id && x.RestaurantId == RestaurantId);
        if (coupon is null) return NotFound();
        coupon.Active = !coupon.Active;
        await db.SaveChangesAsync();
        return Ok(coupon);
    }

    private static string? ValidateCustomer(string? name, string? phone, string? email, DateTime? birthDate)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Informe o nome do cliente.";
        if (name.Trim().Length > 160) return "O nome do cliente deve ter no máximo 160 caracteres.";
        var normalizedPhone = NormalizePhone(phone);
        if (normalizedPhone is not null && (normalizedPhone.Length < 8 || normalizedPhone.Length > 20))
            return "Informe um telefone válido.";
        if (!string.IsNullOrWhiteSpace(email))
        {
            if (email.Trim().Length > 180) return "O e-mail deve ter no máximo 180 caracteres.";
            try { _ = new MailAddress(email.Trim()); }
            catch { return "Informe um e-mail válido."; }
        }
        if (birthDate is DateTime date && date.Date > DateTime.UtcNow.Date)
            return "A data de nascimento não pode estar no futuro.";
        return null;
    }

    private static string? NormalizeEmail(string? email) => string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
    private static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var value = new string(phone.Where(c => char.IsDigit(c) || c == '+').ToArray());
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

public record CreateCustomerRequest(string? Name, string? Phone, string? Email, DateTime? BirthDate);
public record UpdateCustomerRequest(string? Name, string? Phone, string? Email, DateTime? BirthDate, bool Active);
public record LoyaltyAdjustmentRequest(int Points, decimal Cashback, string? Description);
public record CreateCouponRequest(string? Code, string? Description, string? DiscountType, decimal Value, decimal MinimumOrderValue, int? MaxUses, DateTime? ExpiresAt);
