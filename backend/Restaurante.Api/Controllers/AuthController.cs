using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurante.Api.Data;
using Restaurante.Api.Services;

namespace Restaurante.Api.Controllers;
[ApiController, Route("api/auth")]
public class AuthController(AppDbContext db, JwtService jwt) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Email == req.Email && x.Active);
        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash)) return Unauthorized(new { message = "E-mail ou senha inválidos." });
        return Ok(new { token = jwt.Create(user), user = new { user.Id, user.Name, user.Email, user.Role, user.RestaurantId } });
    }
}
public record LoginRequest(string Email, string Password);
