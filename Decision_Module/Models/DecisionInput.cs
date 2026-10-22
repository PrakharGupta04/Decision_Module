
namespace Decision_Module.Models;

public class ReasonInput
{
	public int ReasonId { get; set; }

	public List<int> SubReasonIds { get; set; } = new();
}

public class DecisionInput
{
	public int? DecisionId { get; set; }

	// New: multiple reasons, each with its own sub-reasons.
	public List<ReasonInput> Reasons { get; set; } = new();

	// Legacy properties retained temporarily for compatibility.
	public int? ReasonId { get; set; }

	public List<int> SubReasonIds { get; set; } = new();
}

public class GlobalInput : DecisionInput
{
	public string Kind { get; set; } = "";

	public bool On { get; set; }

	public string? Notes { get; set; }
}

public class CoverInput : DecisionInput
{
	public int UwId { get; set; }

	public string Source { get; set; } = "OVERRIDE";

	public int? PostponePeriod { get; set; }

	public decimal? EMR { get; set; }

	public decimal? OccupationExtra { get; set; }

	public decimal? ResidentialExtra { get; set; }

	public decimal? FlatExtra { get; set; }

	public decimal? AgeExtra { get; set; }

	public string? Notes { get; set; }
}
