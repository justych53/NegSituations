namespace backend.Models;

public class ParticipantMatrix
{
    public int Id { get; set; }
    public int FailureRecordId { get; set; }
    public FailureRecord FailureRecord { get; set; } = null!;
    public int ParticipantAId { get; set; }
    public Participant ParticipantA { get; set; } = null!;
    public int ParticipantBId { get; set; }
    public Participant ParticipantB { get; set; } = null!;
    public double Score { get; set; }
}