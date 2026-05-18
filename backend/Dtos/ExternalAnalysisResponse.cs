using System.Text.Json.Serialization;

namespace backend.Dtos;

public class ExternalAnalysisResponse
{
    [JsonPropertyName("participants")]
    public List<ExternalParticipantItem> Participants { get; set; } = new();

    [JsonPropertyName("factors")]
    public List<ExternalFactorItem> Factors { get; set; } = new();

    [JsonPropertyName("consequences")]
    public List<object> Consequences { get; set; } = new();

    [JsonPropertyName("responsibility")]
    public List<ExternalResponsibilityItem> Responsibility { get; set; } = new();

    [JsonPropertyName("ahp")]
    public ExternalAhp Ahp { get; set; } = new();
}

public class ExternalParticipantItem
{
    [JsonPropertyName("text")] public string Text { get; set; } = "";
    [JsonPropertyName("score")] public double Score { get; set; }
    [JsonPropertyName("start")] public int Start { get; set; }
    [JsonPropertyName("end")] public int End { get; set; }
}

public class ExternalFactorItem
{
    [JsonPropertyName("text")] public string Text { get; set; } = "";
    [JsonPropertyName("factor")] public string Factor { get; set; } = "";
    [JsonPropertyName("score")] public double Score { get; set; }
}

public class ExternalResponsibilityItem
{
    [JsonPropertyName("participant")] public string Participant { get; set; } = "";
    [JsonPropertyName("weight")] public double Weight { get; set; }
    [JsonPropertyName("factor_scores")] public Dictionary<string, double> FactorScores { get; set; } = new();
    [JsonPropertyName("matched_fragments")] public List<ExternalFactorItem> MatchedFragments { get; set; } = new();
}

public class ExternalAhp
{
    [JsonPropertyName("level_1_factors")] public Dictionary<string, double> Level1Factors { get; set; } = new();
    [JsonPropertyName("level_2_participants")] public Dictionary<string, double> Level2Participants { get; set; } = new();
    [JsonPropertyName("factor_weight_source")] public string FactorWeightSource { get; set; } = "";
    [JsonPropertyName("factor_source_scores")] public Dictionary<string, double> FactorSourceScores { get; set; } = new();
    [JsonPropertyName("factor_fragment_counts")] public Dictionary<string, int> FactorFragmentCounts { get; set; } = new();
    [JsonPropertyName("factor_consistency_ratio")] public double FactorConsistencyRatio { get; set; }
    [JsonPropertyName("local_weights_by_factor")] public Dictionary<string, Dictionary<string, double>> LocalWeightsByFactor { get; set; } = new();
    [JsonPropertyName("factor_matrix")] public ExternalMatrix FactorMatrix { get; set; } = new();
    [JsonPropertyName("participant_matrices_by_factor")] public Dictionary<string, ExternalMatrix> ParticipantMatricesByFactor { get; set; } = new();
}

public class ExternalMatrix
{
    [JsonPropertyName("labels")] public List<string> Labels { get; set; } = new();
    [JsonPropertyName("matrix")] public List<List<double>> Matrix { get; set; } = new();
    [JsonPropertyName("weights")] public Dictionary<string, double> Weights { get; set; } = new();
    [JsonPropertyName("source_scores")] public Dictionary<string, double> SourceScores { get; set; } = new();
    [JsonPropertyName("lambda_max")] public double LambdaMax { get; set; }
    [JsonPropertyName("consistency_index")] public double ConsistencyIndex { get; set; }
    [JsonPropertyName("consistency_ratio")] public double? ConsistencyRatio { get; set; }
}