namespace backend.Models;

public class FailureFactor
{
    public int FailureRecordId { get; set; }
    public FailureRecord FailureRecord { get; set; } = null!;
    public int FactorId { get; set; }
    public Factor Factor { get; set; } = null!;
}