namespace backend.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "Expert"; // "Admin" или "Expert"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<FailureRecord> FailureRecords { get; set; } = new List<FailureRecord>();
}