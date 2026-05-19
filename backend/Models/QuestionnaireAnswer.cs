namespace backend.Models;

public class QuestionnaireAnswer
{
    public int Id { get; set; }
    public int ParticipantId { get; set; }
    public Participant Participant { get; set; } = null!;
    public string Answer { get; set; } = string.Empty;
}