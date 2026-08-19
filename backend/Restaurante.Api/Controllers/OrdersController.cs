using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurante.Api.Data;
using Restaurante.Api.Models;
namespace Restaurante.Api.Controllers;
[ApiController,Route("api/orders"),Authorize]
public class OrdersController(AppDbContext db):ControllerBase
{
 private Guid RestaurantId=>Guid.Parse(User.FindFirst("restaurantId")!.Value);
 [HttpGet] public async Task<IActionResult> Get([FromQuery]string? status)=>Ok(await db.Orders.Include(x=>x.Items).Where(x=>x.RestaurantId==RestaurantId&&(status==null||x.Status==status)).OrderByDescending(x=>x.CreatedAt).Take(100).ToListAsync());
 [HttpPost] public async Task<IActionResult> Create(CreateOrderRequest req){var ids=req.Items.Select(x=>x.ProductId).ToList();var products=await db.Products.Where(x=>x.RestaurantId==RestaurantId&&ids.Contains(x.Id)&&x.Active).ToDictionaryAsync(x=>x.Id);if(req.Items.Count==0||req.Items.Any(x=>!products.ContainsKey(x.ProductId)||x.Quantity<=0))return BadRequest(new{message="Itens inválidos."});var order=new Order{RestaurantId=RestaurantId,TableId=req.TableId,CustomerName=req.CustomerName,UserId=Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,out var uid)?uid:null};foreach(var item in req.Items){var p=products[item.ProductId];order.Items.Add(new OrderItem{OrderId=order.Id,ProductId=p.Id,Quantity=item.Quantity,UnitPrice=p.Price,Notes=item.Notes});order.Total+=p.Price*item.Quantity;}db.Orders.Add(order);if(order.TableId is Guid tid){var table=await db.Tables.SingleOrDefaultAsync(x=>x.Id==tid&&x.RestaurantId==RestaurantId);if(table!=null)table.Status="OCCUPIED";}await db.SaveChangesAsync();return Created($"/api/orders/{order.Id}",order);}
 [HttpPost("{id:guid}/close")] public async Task<IActionResult> Close(Guid id,CloseOrderRequest req){var o=await db.Orders.Include(x=>x.Items).SingleOrDefaultAsync(x=>x.Id==id&&x.RestaurantId==RestaurantId);if(o is null)return NotFound();if(o.Status=="CLOSED")return BadRequest(new{message="Pedido já fechado."});o.Status="CLOSED";o.PaymentMethod=req.PaymentMethod;o.ClosedAt=DateTime.UtcNow;db.CashMovements.Add(new CashMovement{RestaurantId=RestaurantId,Type="IN",Description=$"Pedido {o.Id}",Amount=o.Total,PaymentMethod=o.PaymentMethod});if(o.TableId is Guid tid){var t=await db.Tables.SingleOrDefaultAsync(x=>x.Id==tid);if(t!=null)t.Status="AVAILABLE";}await db.SaveChangesAsync();return Ok(o);}
}
public record CreateOrderRequest(Guid? TableId,string? CustomerName,List<CreateOrderItem> Items);
public record CreateOrderItem(Guid ProductId,int Quantity,string? Notes);
public record CloseOrderRequest(string PaymentMethod);
