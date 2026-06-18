using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;
using backend.Services;
using System.Security.Claims;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly LogService _logService;

    public UsersController(AppDbContext context, LogService logService)
    {
        _context = context;
        _logService = logService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetUsers()
    {
        var username = User.FindFirst(ClaimTypes.Name)?.Value;

        return await _context.Users
            .Select(u => new { u.Id, u.Username, u.Role, u.CreatedAt })
            .ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult> CreateUser([FromBody] CreateUserDto dto)
    {
        var currentUsername = User.FindFirst(ClaimTypes.Name)?.Value;

        if (await _context.Users.AnyAsync(u => u.Username == dto.Username))
        {
            await _logService.LogAsync("Warning", currentUsername, 
                "Попытка создать существующего пользователя", $"user={dto.Username}");
            return BadRequest("Пользователь уже существует");
        }

        var user = new User
        {
            Username = dto.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = "Expert",
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        await _logService.LogAsync("Info", currentUsername, 
            "Создание эксперта", $"user={user.Username}, id={user.Id}");

        return Ok(new { user.Id, user.Username, user.Role });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var currentUsername = User.FindFirst(ClaimTypes.Name)?.Value;
        var user = await _context.Users.FindAsync(id);

        if (user == null)
        {
            await _logService.LogAsync("Warning", currentUsername, 
                "Попытка удалить несуществующего пользователя", $"id={id}");
            return NotFound();
        }

        if (user.Role == "Admin")
        {
            await _logService.LogAsync("Warning", currentUsername, 
                "Попытка удалить администратора", $"id={id}, username={user.Username}");
            return BadRequest("Нельзя удалить администратора");
        }

        var deletedUsername = user.Username;
        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        await _logService.LogAsync("Warning", currentUsername, 
            "Удаление пользователя", $"user={deletedUsername}, id={id}");

        return NoContent();
    }
}

public class CreateUserDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}