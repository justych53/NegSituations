using System.Text.Json.Serialization;

namespace backend.Dtos;

public class ExternalParticipantDto
{
    [JsonPropertyName("participant")]
    public string Participant { get; set; } = string.Empty;
    public string ParticipationType { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public string Factor { get; set; } = string.Empty;
    public string Fragment { get; set; } = string.Empty;
    public double Score { get; set; }
}