namespace backend.Models;

public class FailureRecord
{
    public int Id { get; set; }
    public string DescFailure { get; set; } = string.Empty;
    public string ResInvest { get; set; } = string.Empty;
    public List<FailureParticipant> FailureParticipants { get; set; } = new();
}