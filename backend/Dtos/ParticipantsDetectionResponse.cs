using System.Text.Json.Serialization;

namespace backend.Dtos;

public class ParticipantsDetectionResponse
{
    [JsonPropertyName("participants")]
    public List<DetectedParticipant> Participants { get; set; } = new();

    [JsonPropertyName("count")]
    public int Count { get; set; }
}

public class DetectedParticipant
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("score")]
    public double Score { get; set; }

    [JsonPropertyName("start")]
    public int Start { get; set; }

    [JsonPropertyName("end")]
    public int End { get; set; }
}