namespace backend.Models;

public class Participant
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public int FailureRecordId { get; set; }
    public FailureRecord FailureRecord { get; set; } = null!;
}