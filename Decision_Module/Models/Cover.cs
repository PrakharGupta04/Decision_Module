using System.Globalization;

namespace Decision_Module.Models;

public class Cover
{
	public int UwId { get; set; }
	public string CustomerId { get; set; } = "";
	public string CoverType { get; set; } = "";      // BASE or RIDER
	public string PolicyNumber { get; set; } = "";
	public string? RiderId { get; set; }
	public string? RiderType { get; set; }

	public string MarketName { get; set; } = "";
	public int? BillingFrequency { get; set; }
	public string BenefitCode { get; set; } = "";
	public string? BenefitGroup { get; set; }
	public int? Bterm { get; set; }
	public int? Bppt { get; set; }
	public decimal? BenefitInstalmentPremium { get; set; }
	public decimal? BenefitSumAssured { get; set; }

	public int? DecisionId { get; set; }
	public string? DecisionName { get; set; }
	public int? ReasonId { get; set; }
	public string? ReasonText { get; set; }
	public string? DecisionSource { get; set; }       // GLOBAL, OVERRIDE or null (no decision yet)
	public int? PostponePeriod { get; set; }
	public decimal? EMR { get; set; }
	public decimal? OccupationExtra { get; set; }
	public decimal? ResidentialExtra { get; set; }
	public decimal? FlatExtra { get; set; }
	public decimal? AgeExtra { get; set; }
	public string? Notes { get; set; }

	public string SourceLabel => DecisionSource switch
	{
		"GLOBAL" => "Global",
		"OVERRIDE" => "Override",
		_ => ""
	};

	// short labels for the details that are actually set; empty ones are skipped
	public IEnumerable<string> Details()
	{
		if (PostponePeriod is not null) yield return $"Postpone period {PostponePeriod}";
		if (EMR is not null) yield return $"EMR {Fmt(EMR.Value)}";
		if (OccupationExtra is not null) yield return $"Occupation extra {Fmt(OccupationExtra.Value)}";
		if (ResidentialExtra is not null) yield return $"Residential extra {Fmt(ResidentialExtra.Value)}";
		if (FlatExtra is not null) yield return $"Flat extra {Fmt(FlatExtra.Value)}";
		if (AgeExtra is not null) yield return $"Age extra {Fmt(AgeExtra.Value)}";
	}

	private static string Fmt(decimal v) => v.ToString("0.##", CultureInfo.InvariantCulture);
}