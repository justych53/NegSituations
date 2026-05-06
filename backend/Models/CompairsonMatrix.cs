namespace backend.Models;

public class ComparisonMatrix
{
    public int Id { get; set; }
    public int FailureRecordId { get; set; }
    public FailureRecord FailureRecord { get; set; } = null!;
    public int FactorAId { get; set; }
    public Factor FactorA { get; set; } = null!;
    public int FactorBId { get; set; }
    public Factor FactorB { get; set; } = null!;
    public double Score { get; set; } 
}