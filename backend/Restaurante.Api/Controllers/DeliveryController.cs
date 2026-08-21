using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurante.Api.Data;

namespace Restaurante.Api.Controllers;

[ApiController, Route("api/delivery"), Authorize]
public class DeliveryController(AppDbContext db) : ControllerBase
{
    private Guid RestaurantId => Guid.Parse(User.FindFirst("restaurantId")!.Value);

    private Task EnsureTables() => db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS delivery_zones (
            id UUID PRIMARY KEY,
            restaurant_id UUID NOT NULL REFERENCES restaurants(id),
            name VARCHAR(120) NOT NULL,
            fee NUMERIC(12,2) NOT NULL DEFAULT 0,
            estimated_minutes INTEGER NOT NULL DEFAULT 45,
            active BOOLEAN NOT NULL DEFAULT TRUE,
            created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );
        CREATE UNIQUE INDEX IF NOT EXISTS delivery_zones_restaurant_name_key
            ON delivery_zones(restaurant_id, name);

        CREATE TABLE IF NOT EXISTS delivery_drivers (
            id UUID PRIMARY KEY,
            restaurant_id UUID NOT NULL REFERENCES restaurants(id),
            name VARCHAR(160) NOT NULL,
            phone VARCHAR(30),
            vehicle VARCHAR(80),
            active BOOLEAN NOT NULL DEFAULT TRUE,
            created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );
        CREATE INDEX IF NOT EXISTS idx_delivery_drivers_restaurant_active
            ON delivery_drivers(restaurant_id, active);

        CREATE TABLE IF NOT EXISTS delivery_orders (
            id UUID PRIMARY KEY,
            restaurant_id UUID NOT NULL REFERENCES restaurants(id),
            order_id UUID NOT NULL UNIQUE REFERENCES orders(id) ON DELETE CASCADE,
            zone_id UUID NOT NULL REFERENCES delivery_zones(id),
            driver_id UUID NULL REFERENCES delivery_drivers(id),
            address VARCHAR(240) NOT NULL,
            number VARCHAR(30),
            complement VARCHAR(120),
            neighborhood VARCHAR(120) NOT NULL,
            reference VARCHAR(180),
            delivery_fee NUMERIC(12,2) NOT NULL DEFAULT 0,
            status VARCHAR(30) NOT NULL DEFAULT 'WAITING',
            created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            dispatched_at TIMESTAMPTZ,
            delivered_at TIMESTAMPTZ
        );
        CREATE INDEX IF NOT EXISTS idx_delivery_orders_restaurant_status
            ON delivery_orders(restaurant_id, status, created_at);
        """);

    [HttpGet("zones")]
    public async Task<IActionResult> Zones()
    {
        await EnsureTables();
        var rows = await db.Database.SqlQueryRaw<DeliveryZoneRow>("""
            SELECT id AS "Id", name AS "Name", fee AS "Fee",
                   estimated_minutes AS "EstimatedMinutes", active AS "Active"
            FROM delivery_zones
            WHERE restaurant_id = {0}
            ORDER BY active DESC, name
            """, RestaurantId).ToListAsync();
        return Ok(rows);
    }

    [HttpPost("zones")]
    public async Task<IActionResult> CreateZone(CreateZoneRequest req)
    {
        await EnsureTables();
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest(new { message = "Informe o nome da região." });
        if (req.Fee < 0) return BadRequest(new { message = "A taxa não pode ser negativa." });
        var id = Guid.NewGuid();
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO delivery_zones(id, restaurant_id, name, fee, estimated_minutes, active)
                VALUES ({id}, {RestaurantId}, {req.Name.Trim()}, {req.Fee}, {Math.Max(10, req.EstimatedMinutes)}, TRUE)
                """);
        }
        catch
        {
            return Conflict(new { message = "Já existe uma região com este nome." });
        }
        return Created($"/api/delivery/zones/{id}", new { id });
    }

    [HttpPatch("zones/{id:guid}/toggle")]
    public async Task<IActionResult> ToggleZone(Guid id)
    {
        await EnsureTables();
        var affected = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE delivery_zones SET active = NOT active
            WHERE id = {id} AND restaurant_id = {RestaurantId}
            """);
        return affected == 0 ? NotFound() : NoContent();
    }

    [HttpGet("drivers")]
    public async Task<IActionResult> Drivers()
    {
        await EnsureTables();
        var rows = await db.Database.SqlQueryRaw<DeliveryDriverRow>("""
            SELECT id AS "Id", name AS "Name", phone AS "Phone", vehicle AS "Vehicle", active AS "Active"
            FROM delivery_drivers
            WHERE restaurant_id = {0}
            ORDER BY active DESC, name
            """, RestaurantId).ToListAsync();
        return Ok(rows);
    }

    [HttpPost("drivers")]
    public async Task<IActionResult> CreateDriver(CreateDriverRequest req)
    {
        await EnsureTables();
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest(new { message = "Informe o nome do entregador." });
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO delivery_drivers(id, restaurant_id, name, phone, vehicle, active)
            VALUES ({id}, {RestaurantId}, {req.Name.Trim()}, {req.Phone}, {req.Vehicle}, TRUE)
            """);
        return Created($"/api/delivery/drivers/{id}", new { id });
    }

    [HttpPatch("drivers/{id:guid}/toggle")]
    public async Task<IActionResult> ToggleDriver(Guid id)
    {
        await EnsureTables();
        var affected = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE delivery_drivers SET active = NOT active
            WHERE id = {id} AND restaurant_id = {RestaurantId}
            """);
        return affected == 0 ? NotFound() : NoContent();
    }

    [HttpGet("orders")]
    public async Task<IActionResult> Orders()
    {
        await EnsureTables();
        var rows = await db.Database.SqlQueryRaw<DeliveryOrderRow>("""
            SELECT d.id AS "Id", d.order_id AS "OrderId", d.zone_id AS "ZoneId",
                   z.name AS "ZoneName", d.driver_id AS "DriverId", dr.name AS "DriverName",
                   d.address AS "Address", d.number AS "Number", d.complement AS "Complement",
                   d.neighborhood AS "Neighborhood", d.reference AS "Reference",
                   d.delivery_fee AS "DeliveryFee", d.status AS "Status",
                   d.created_at AS "CreatedAt", d.dispatched_at AS "DispatchedAt", d.delivered_at AS "DeliveredAt",
                   o.customer_name AS "CustomerName", o.total AS "OrderTotal", o.status AS "OrderStatus"
            FROM delivery_orders d
            JOIN delivery_zones z ON z.id = d.zone_id
            JOIN orders o ON o.id = d.order_id
            LEFT JOIN delivery_drivers dr ON dr.id = d.driver_id
            WHERE d.restaurant_id = {0}
            ORDER BY CASE d.status WHEN 'OUT_FOR_DELIVERY' THEN 0 WHEN 'WAITING' THEN 1 ELSE 2 END,
                     d.created_at DESC
            LIMIT 200
            """, RestaurantId).ToListAsync();
        return Ok(rows);
    }

    [HttpPost("orders")]
    public async Task<IActionResult> AttachOrder(CreateDeliveryRequest req)
    {
        await EnsureTables();
        var order = await db.Orders.SingleOrDefaultAsync(x => x.Id == req.OrderId && x.RestaurantId == RestaurantId);
        if (order is null) return NotFound(new { message = "Pedido não encontrado." });
        if (order.Status != "NEW") return BadRequest(new { message = "Só é possível transformar um pedido novo em delivery." });
        if (string.IsNullOrWhiteSpace(req.Address) || string.IsNullOrWhiteSpace(req.Neighborhood))
            return BadRequest(new { message = "Informe endereço e bairro." });

        var zone = await db.Database.SqlQueryRaw<DeliveryZoneRow>("""
            SELECT id AS "Id", name AS "Name", fee AS "Fee",
                   estimated_minutes AS "EstimatedMinutes", active AS "Active"
            FROM delivery_zones WHERE id = {0} AND restaurant_id = {1}
            """, req.ZoneId, RestaurantId).SingleOrDefaultAsync();
        if (zone is null || !zone.Active) return BadRequest(new { message = "Região de entrega inválida ou inativa." });

        var exists = await db.Database.SqlQueryRaw<int>("SELECT COUNT(*)::int AS \"Value\" FROM delivery_orders WHERE order_id = {0}", req.OrderId).SingleAsync();
        if (exists > 0) return Conflict(new { message = "Este pedido já está vinculado ao delivery." });

        var id = Guid.NewGuid();
        await using var tx = await db.Database.BeginTransactionAsync();
        order.Total += zone.Fee;
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO delivery_orders(id, restaurant_id, order_id, zone_id, address, number, complement, neighborhood, reference, delivery_fee, status)
            VALUES ({id}, {RestaurantId}, {req.OrderId}, {req.ZoneId}, {req.Address.Trim()}, {req.Number}, {req.Complement}, {req.Neighborhood.Trim()}, {req.Reference}, {zone.Fee}, 'WAITING')
            """);
        await tx.CommitAsync();
        return Created($"/api/delivery/orders/{id}", new { id, deliveryFee = zone.Fee, total = order.Total });
    }

    [HttpPatch("orders/{id:guid}/driver")]
    public async Task<IActionResult> AssignDriver(Guid id, AssignDriverRequest req)
    {
        await EnsureTables();
        var driver = await db.Database.SqlQueryRaw<int>("SELECT COUNT(*)::int AS \"Value\" FROM delivery_drivers WHERE id = {0} AND restaurant_id = {1} AND active = TRUE", req.DriverId, RestaurantId).SingleAsync();
        if (driver == 0) return BadRequest(new { message = "Entregador inválido ou inativo." });
        var affected = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE delivery_orders SET driver_id = {req.DriverId}
            WHERE id = {id} AND restaurant_id = {RestaurantId} AND status = 'WAITING'
            """);
        return affected == 0 ? BadRequest(new { message = "Entrega não encontrada ou já despachada." }) : NoContent();
    }

    [HttpPatch("orders/{id:guid}/status")]
    public async Task<IActionResult> ChangeStatus(Guid id, ChangeDeliveryStatusRequest req)
    {
        await EnsureTables();
        var delivery = await db.Database.SqlQueryRaw<DeliveryStatusRow>("""
            SELECT id AS "Id", order_id AS "OrderId", driver_id AS "DriverId", status AS "Status"
            FROM delivery_orders WHERE id = {0} AND restaurant_id = {1}
            """, id, RestaurantId).SingleOrDefaultAsync();
        if (delivery is null) return NotFound();

        var requested = req.Status.Trim().ToUpperInvariant();
        var order = await db.Orders.SingleAsync(x => x.Id == delivery.OrderId && x.RestaurantId == RestaurantId);

        if (requested == "OUT_FOR_DELIVERY")
        {
            if (delivery.Status != "WAITING") return BadRequest(new { message = "A entrega já saiu ou foi finalizada." });
            if (delivery.DriverId is null) return BadRequest(new { message = "Selecione um entregador antes de despachar." });
            if (order.Status != "READY") return BadRequest(new { message = "O pedido precisa estar PRONTO na cozinha antes de sair para entrega." });
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE delivery_orders SET status = 'OUT_FOR_DELIVERY', dispatched_at = NOW()
                WHERE id = {id} AND restaurant_id = {RestaurantId}
                """);
            return NoContent();
        }

        if (requested == "DELIVERED")
        {
            if (delivery.Status != "OUT_FOR_DELIVERY") return BadRequest(new { message = "A entrega precisa estar em rota." });
            await using var tx = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE delivery_orders SET status = 'DELIVERED', delivered_at = NOW()
                WHERE id = {id} AND restaurant_id = {RestaurantId}
                """);
            order.Status = "DELIVERED";
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return NoContent();
        }

        return BadRequest(new { message = "Status inválido. Use OUT_FOR_DELIVERY ou DELIVERED." });
    }
}

public record CreateZoneRequest(string Name, decimal Fee, int EstimatedMinutes);
public record CreateDriverRequest(string Name, string? Phone, string? Vehicle);
public record CreateDeliveryRequest(Guid OrderId, Guid ZoneId, string Address, string? Number, string? Complement, string Neighborhood, string? Reference);
public record AssignDriverRequest(Guid DriverId);
public record ChangeDeliveryStatusRequest(string Status);
public class DeliveryZoneRow { public Guid Id { get; set; } public string Name { get; set; } = ""; public decimal Fee { get; set; } public int EstimatedMinutes { get; set; } public bool Active { get; set; } }
public class DeliveryDriverRow { public Guid Id { get; set; } public string Name { get; set; } = ""; public string? Phone { get; set; } public string? Vehicle { get; set; } public bool Active { get; set; } }
public class DeliveryStatusRow { public Guid Id { get; set; } public Guid OrderId { get; set; } public Guid? DriverId { get; set; } public string Status { get; set; } = ""; }
public class DeliveryOrderRow
{
    public Guid Id { get; set; } public Guid OrderId { get; set; } public Guid ZoneId { get; set; } public string ZoneName { get; set; } = "";
    public Guid? DriverId { get; set; } public string? DriverName { get; set; } public string Address { get; set; } = ""; public string? Number { get; set; }
    public string? Complement { get; set; } public string Neighborhood { get; set; } = ""; public string? Reference { get; set; }
    public decimal DeliveryFee { get; set; } public string Status { get; set; } = ""; public DateTime CreatedAt { get; set; }
    public DateTime? DispatchedAt { get; set; } public DateTime? DeliveredAt { get; set; } public string? CustomerName { get; set; }
    public decimal OrderTotal { get; set; } public string OrderStatus { get; set; } = "";
}
