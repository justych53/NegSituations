namespace backend.Models;

public class FailureRecord
{
    public int Id { get; set; }
    public string DescFailure { get; set; } = string.Empty;
    public string ResInvest { get; set; } = string.Empty;
    public List<Participant> Participants { get; set; } = new();
    public List<FailureFactor> FailureFactors { get; set; } = new();
}