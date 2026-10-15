namespace Decision_Module.Models;

public class Policy
{
	public string PolicyNumber { get; set; } = "";
	public string CustomerId { get; set; } = "";
	public string MarketName { get; set; } = "";
	public int? BillingFrequency { get; set; }
	public string BenefitCode { get; set; } = "";
	public string? BenefitGroup { get; set; }
	public int? Bterm { get; set; }
	public int? Bppt { get; set; }
	public decimal? BenefitInstalmentPremium { get; set; }
	public decimal? BenefitSumAssured { get; set; }

	// from the vCoverDecision view
	public string EffectiveDecision { get; set; } = "No decision yet";
	public string Source { get; set; } = "-";
}