namespace backend.Models;

public class LogEntry
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Level { get; set; } = "Info"; // Info, Warning, Error
    public string? Username { get; set; }
    public string? Action { get; set; }
    public string? Details { get; set; }
}