using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurante.Api.Data;
using Restaurante.Api.Models;

namespace Restaurante.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "ADMIN")]
public class UsersController(AppDbContext db) : ControllerBase
{
    private static readonly string[] AllowedRoles = ["ADMIN", "MANAGER", "CASHIER", "WAITER", "KITCHEN"];
    private Guid RestaurantId => Guid.Parse(User.FindFirst("restaurantId")!.Value);
    private Guid CurrentUserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var users = await db.Users
            .Where(x => x.RestaurantId == RestaurantId)
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name, x.Email, x.Role, x.Active })
            .ToListAsync();
        return Ok(users);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserRequest req)
    {
        var name = req.Name?.Trim();
        var email = req.Email?.Trim().ToLowerInvariant();
        var role = req.Role?.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(name)) return BadRequest(new { message = "Informe o nome do usuário." });
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) return BadRequest(new { message = "Informe um e-mail válido." });
        if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 8) return BadRequest(new { message = "A senha deve ter pelo menos 8 caracteres." });
        if (role is null || !AllowedRoles.Contains(role)) return BadRequest(new { message = "Perfil inválido." });
        if (await db.Users.AnyAsync(x => x.Email == email)) return Conflict(new { message = "Já existe um usuário com este e-mail." });

        var user = new AppUser
        {
            RestaurantId = RestaurantId,
            Name = name,
            Email = email,
            Role = role,
            Active = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password)
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return Created($"/api/users/{user.Id}", new { user.Id, user.Name, user.Email, user.Role, user.Active });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateUserRequest req)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id && x.RestaurantId == RestaurantId);
        if (user is null) return NotFound(new { message = "Usuário não encontrado." });

        var role = req.Role?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest(new { message = "Informe o nome do usuário." });
        if (role is null || !AllowedRoles.Contains(role)) return BadRequest(new { message = "Perfil inválido." });
        if (id == CurrentUserId && !req.Active) return BadRequest(new { message = "Você não pode desativar seu próprio usuário." });
        if (id == CurrentUserId && role != "ADMIN") return BadRequest(new { message = "Você não pode remover seu próprio perfil de administrador." });

        user.Name = req.Name.Trim();
        user.Role = role;
        user.Active = req.Active;
        await db.SaveChangesAsync();
        return Ok(new { user.Id, user.Name, user.Email, user.Role, user.Active });
    }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, ResetPasswordRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 8)
            return BadRequest(new { message = "A nova senha deve ter pelo menos 8 caracteres." });

        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id && x.RestaurantId == RestaurantId);
        if (user is null) return NotFound(new { message = "Usuário não encontrado." });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        await db.SaveChangesAsync();
        return Ok(new { message = "Senha redefinida com sucesso." });
    }
}

public record CreateUserRequest(string Name, string Email, string Password, string Role);
public record UpdateUserRequest(string Name, string Role, bool Active);
public record ResetPasswordRequest(string NewPassword);
