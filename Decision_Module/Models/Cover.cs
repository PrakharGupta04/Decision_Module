namespace Decision_Module.Models;

// One row of UWDecision: a policy's base cover or one of its riders, with its saved decision
public class Cover
{
    public int UwId { get; set; }
    public string CoverType { get; set; } = "";
    public string PolicyNumber { get; set; } = "";
    public string? RiderId { get; set; }
    public string? RiderType { get; set; }
    public string? RiderTypeName { get; set; }

    public string MarketName { get; set; } = "";
    public string? BenefitGroup { get; set; }
    public string BenefitCode { get; set; } = "";
    public int? Bterm { get; set; }
    public int? Bppt { get; set; }
    public decimal? BenefitInstalmentPremium { get; set; }
    public decimal? BenefitSumAssured { get; set; }
    public string? PaymentFrequency { get; set; }
    public string? FrequencyLabel { get; set; }
    public string? ReinsurerId { get; set; }
    public string? BaseCoverId { get; set; }
    public string? BaseCoverType { get; set; }

    public string? DecisionSource { get; set; }
    public int? DecisionId { get; set; }
    public int? ReasonId { get; set; }
    public int? PostponePeriod { get; set; }
    public decimal? EMR { get; set; }
    public decimal? OccupationExtra { get; set; }
    public decimal? ResidentialExtra { get; set; }
    public decimal? FlatExtra { get; set; }
    public decimal? AgeExtra { get; set; }
    public string? Notes { get; set; }
}