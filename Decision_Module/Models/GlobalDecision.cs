namespace Decision_Module.Models;

public class GlobalDecision
{
    public string CoverType { get; set; } = "";
    public int DecisionId { get; set; }
    public string DecisionName { get; set; } = "";
    public string? ReasonText { get; set; }
}
