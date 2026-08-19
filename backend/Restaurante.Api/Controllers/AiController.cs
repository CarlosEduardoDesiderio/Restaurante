using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurante.Api.Data;
namespace Restaurante.Api.Controllers;
[ApiController,Route("api/ai"),Authorize]
public class AiController(AppDbContext db,IConfiguration config):ControllerBase
{
 private Guid RestaurantId=>Guid.Parse(User.FindFirst("restaurantId")!.Value);
 [HttpPost("ask")] public async Task<IActionResult> Ask([FromBody]AiQuestion request){var q=request.Question.ToLowerInvariant();if(q.Contains("faturamento")||q.Contains("vendas")){var today=DateTime.UtcNow.Date;var value=await db.Orders.Where(x=>x.RestaurantId==RestaurantId&&x.Status=="CLOSED"&&x.CreatedAt>=today).SumAsync(x=>(decimal?)x.Total)??0;return Ok(new{answer=$"O faturamento de hoje é {value:C2}.",source="database"});}if(q.Contains("estoque")){var critical=await db.Ingredients.Where(x=>x.RestaurantId==RestaurantId&&x.CurrentQuantity<=x.MinimumQuantity).Select(x=>x.Name).ToListAsync();return Ok(new{answer=critical.Count==0?"Não há ingredientes em estoque crítico.":$"Estoque crítico: {string.Join(", ",critical)}.",source="database"});}return Ok(new{answer="Entendi sua pergunta. A integração com Azure OpenAI está preparada, mas esta pergunta ainda precisa de uma intenção configurada.",source="fallback"});}
}
public record AiQuestion(string Question);
