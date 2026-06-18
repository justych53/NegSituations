using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class LogsController : ControllerBase
{
    private readonly AppDbContext _context;

    public LogsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<object>> GetLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? level = null,
        [FromQuery] string? username = null)
    {
        IQueryable<Models.LogEntry> query = _context.LogEntries;

        if (!string.IsNullOrWhiteSpace(level))
            query = query.Where(l => l.Level == level);
        if (!string.IsNullOrWhiteSpace(username))
            query = query.Where(l => l.Username != null && l.Username.Contains(username));

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(l => l.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new
            {
                l.Id,
                l.Timestamp,
                l.Level,
                l.Username,
                l.Action,
                l.Details
            })
            .ToListAsync();

        return Ok(new { items, totalCount });
    }
}