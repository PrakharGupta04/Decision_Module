namespace Decision_Module.Models;

public class DecisionReason
{
    public int ReasonId { get; set; }
    public int DecisionId { get; set; }
    public string ReasonText { get; set; } = "";
    public string Kinds { get; set; } = "";   // e.g. ",BASE,Critical_Illness,"
}