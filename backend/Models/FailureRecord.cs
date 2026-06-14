namespace backend.Models;

public class FailureRecord
{
    public int Id { get; set; }
    public string DescFailure { get; set; } = string.Empty;
    public string ResInvest { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedByUserId { get; set; }
    public User? CreatedBy { get; set; }

    public List<Participant> Participants { get; set; } = new();
    public List<FailureFactor> FailureFactors { get; set; } = new();
    public List<ComparisonMatrix> ComparisonMatrices { get; set; } = new();
    public List<ParticipantMatrix> ParticipantMatrices { get; set; } = new();
}