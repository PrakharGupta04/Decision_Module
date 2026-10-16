// Models/DecisionReason.cs
namespace Decision_Module.Models;

public class DecisionReason
{
	public int ReasonId { get; set; }
	public int DecisionId { get; set; }
	public string ReasonText { get; set; } = "";
}