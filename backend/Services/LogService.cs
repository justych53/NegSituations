using backend.Data;
using backend.Models;

namespace backend.Services;

public class LogService
{
    private readonly AppDbContext _context;

    public LogService(AppDbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(string level, string? username, string action, string? details = null)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = level,
            Username = username,
            Action = action,
            Details = details
        };
        _context.LogEntries.Add(entry);
        await _context.SaveChangesAsync();
    }
    
}