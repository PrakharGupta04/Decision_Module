namespace Decision_Module.Models;

public class Rider
{
	public string RiderId { get; set; } = "";
	public string PolicyNumber { get; set; } = "";
	public string RiderType { get; set; } = "";
	public decimal? RiderSumAssured { get; set; }
	public decimal? RiderPremium { get; set; }
	public int? RiderTerm { get; set; }
	public int? RiderPpt { get; set; }

	// from the vCoverDecision view
	public string EffectiveDecision { get; set; } = "No decision yet";
	public string Source { get; set; } = "-";
}