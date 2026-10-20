namespace Decision_Module.Models;

public class GlobalDecision
{
    public int GlobalId { get; set; }
    public string CoverKind { get; set; } = "";
    public bool IsOn { get; set; }
    public int? DecisionId { get; set; }
    public int? ReasonId { get; set; }
}

public class GlobalSubReason
{
    public string CoverKind { get; set; } = "";
    public int SubReasonId { get; set; }
}