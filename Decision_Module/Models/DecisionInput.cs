namespace Decision_Module.Models;

public class DecisionInput
{
    public int? UwId { get; set; }
    public string? CoverType { get; set; }
    public int? DecisionId { get; set; }
    public string? ReasonText { get; set; }
    public int? PostponePeriod { get; set; }
    public decimal? EMR { get; set; }
    public decimal? OccupationExtra { get; set; }
    public decimal? ResidentialExtra { get; set; }
    public decimal? FlatExtra { get; set; }
    public decimal? AgeExtra { get; set; }
    public string? Notes { get; set; }
}
