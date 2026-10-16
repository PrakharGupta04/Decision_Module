namespace Decision_Module.Models;

public class DecisionInput
{
	public int? UwId { get; set; }            // which cover (empty for a global decision)
	public string? CoverType { get; set; }    // BASE or RIDER (used by the global cards)
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