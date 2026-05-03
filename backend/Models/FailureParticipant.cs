namespace backend.Models;

public class FailureParticipant
{
    public int FailureRecordId { get; set; }
    public FailureRecord FailureRecord { get; set; } = null!;
    public int ParticipantId { get; set; }
    public Participant Participant { get; set; } = null!;
}