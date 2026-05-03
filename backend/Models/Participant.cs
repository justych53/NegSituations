namespace backend.Models;

public class Participant
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public List<FailureParticipant> FailureParticipants { get; set; } = new();
}