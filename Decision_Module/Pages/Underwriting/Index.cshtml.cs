using System.Globalization;
using Decision_Module.Data;
using Decision_Module.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Decision_Module.Pages.Underwriting;

// what the cover editor partial needs: the cover, and the page it sits on
public record CoverView(Cover Cover, IndexModel Page);

public class IndexModel : PageModel
{
	private readonly UnderwritingData _data;
	private readonly ILogger<IndexModel> _logger;

	private Dictionary<string, HashSet<string>> _decisionFields = new();
	private Dictionary<string, HashSet<string>> _coverExtras = new();

	public IndexModel(UnderwritingData data, ILogger<IndexModel> logger)
	{
		_data = data;
		_logger = logger;
	}

	// ---------- what the page receives ----------

	[BindProperty(SupportsGet = true)]
	public string? CustomerId { get; set; }

	[BindProperty]
	public DecisionInput Form { get; set; } = new();

	[TempData]
	public string? Success { get; set; }

	// ---------- what the page shows ----------

	public Customer? Customer { get; private set; }
	public List<Cover> Covers { get; private set; } = new();
	public List<Cover> Policies { get; private set; } = new();   // the base covers
	public ILookup<string, Cover> RidersByPolicy { get; private set; } =
		Enumerable.Empty<Cover>().ToLookup(c => c.PolicyNumber);
	public List<DecisionOption> Decisions { get; private set; } = new();
	public List<DecisionReason> Reasons { get; private set; } = new();
	public List<string> Messages { get; private set; } = new();
	public string? Warning { get; private set; }
	public string? Error { get; private set; }
	public int? OpenUwId { get; private set; }        // after a failed save, keep that cover's editor open
	public string? OpenPolicy { get; private set; }

	private bool IsPost => HttpMethods.IsPost(Request.Method);

	// ---------- handlers ----------

	public void OnGet()
	{
		// first visit: no search yet
		if (!Request.Query.ContainsKey("CustomerId")) return;
		TryLoadCustomer();
	}

	// Global card: one decision for all of the customer's base covers (or riders), except overrides
	public IActionResult OnPostSaveGlobal()
	{
		if (!TryLoadCustomer()) return Page();

		var type = Form.CoverType ?? "";
		if (type != "BASE" && type != "RIDER")
		{
			Error = "Unknown cover type.";
			return Page();
		}
		if (CoverCount(type) == 0)
		{
			Messages = new() { "This customer has no covers of this type to apply a decision to." };
			return Page();
		}
		if (AffectedCovers(type).Count == 0)
		{
			Messages = new() { "Every cover of this type has its own decision, so there is nothing for a global decision to apply to." };
			return Page();
		}

		var errors = BindingErrors();
		errors.AddRange(ValidateDecision(Form.DecisionId, Form.ReasonId));
		if (errors.Count > 0)
		{
			Messages = errors;
			return Page();
		}

		int changed;
		try
		{
			changed = _data.SaveGlobalDecision(CustomerId!, type, Form.DecisionId!.Value, Form.ReasonId);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Saving global decision failed for {CustomerId}", CustomerId);
			Error = "The decision could not be saved. Nothing was changed.";
			return Page();
		}
		if (changed < 1)
		{
			Error = "The decision could not be saved. Nothing was changed.";
			return Page();
		}

		var name = Decisions.First(d => d.DecisionId == Form.DecisionId).DecisionName;
		var title = type == "BASE" ? "Base cover" : "Riders";
		Success = $"{title} global decision saved: {name}. Applied to {changed} cover(s); " +
				  $"{KeptCovers(type).Count} kept (own decision).";
		return RedirectToPage(new { CustomerId });
	}

	// One cover (base cover or rider) edited by hand
	public IActionResult OnPostSaveCover()
	{
		if (!TryLoadCustomer()) return Page();

		var cover = Covers.FirstOrDefault(c => c.UwId == Form.UwId);
		if (cover is null)
		{
			Error = "That cover was not found for this customer.";
			return Page();
		}
		OpenUwId = cover.UwId;           // if we have to show errors, keep this editor open
		OpenPolicy = cover.PolicyNumber;

		KeepOnlyApplicableFields(cover);
		Form.Notes = string.IsNullOrWhiteSpace(Form.Notes) ? null : Form.Notes.Trim();

		var errors = BindingErrors();
		errors.AddRange(ValidateDecision(Form.DecisionId, Form.ReasonId));
		errors.AddRange(ValidateDetails(Form));
		if (errors.Count > 0)
		{
			Messages = errors;
			return Page();
		}

		int saved;
		try
		{
			saved = _data.SaveCoverDecision(cover.UwId, CustomerId!, Form);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Saving cover {UwId} failed for {CustomerId}", cover.UwId, CustomerId);
			Error = "The decision could not be saved. Nothing was changed.";
			return Page();
		}
		if (saved < 1)
		{
			Error = "The decision could not be saved. Nothing was changed.";
			return Page();
		}

		Success = $"Saved: {CoverLabel(cover)}.";
		return RedirectToPage(new { CustomerId });
	}

	// Reset an overridden cover so it follows the global decision again
	public IActionResult OnPostResetCover()
	{
		if (!TryLoadCustomer()) return Page();

		var cover = Covers.FirstOrDefault(c => c.UwId == Form.UwId);
		if (cover is null)
		{
			Error = "That cover was not found for this customer.";
			return Page();
		}

		Form = new DecisionInput();          // so the page shows saved values, not this tiny form
		OpenUwId = cover.UwId;
		OpenPolicy = cover.PolicyNumber;

		if (cover.DecisionSource != "OVERRIDE")
		{
			Messages = new() { "This cover already follows the global decision." };
			return Page();
		}

		int reset;
		try
		{
			reset = _data.ResetToGlobal(cover.UwId, CustomerId!);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Reset of cover {UwId} failed for {CustomerId}", cover.UwId, CustomerId);
			Error = "The reset could not be saved. Nothing was changed.";
			return Page();
		}
		if (reset < 1)
		{
			Messages = new() { "There is no global decision to return to. Set the global decision for this cover type first." };
			return Page();
		}

		Success = $"Reset: {CoverLabel(cover)} now follows the global decision.";
		return RedirectToPage(new { CustomerId });
	}

	// ---------- loading ----------

	private bool TryLoadCustomer()
	{
		CustomerId = CustomerId?.Trim();

		if (string.IsNullOrEmpty(CustomerId))
		{
			Warning = "Enter a Customer ID.";
			return false;
		}
		if (CustomerId.Length > 20)
		{
			Warning = "Customer ID is too long (maximum 20 characters).";
			return false;
		}

		try
		{
			Customer = _data.GetCustomer(CustomerId);
			if (Customer is null)
			{
				Warning = $"No customer found with ID {CustomerId}.";
				return false;
			}

			Covers = _data.GetCovers(CustomerId);
			Policies = Covers.Where(c => c.CoverType == "BASE").ToList();
			RidersByPolicy = Covers.Where(c => c.CoverType == "RIDER").ToLookup(c => c.PolicyNumber);
			Decisions = _data.GetDecisionOptions();
			Reasons = _data.GetReasons();
			_decisionFields = _data.GetDecisionFields();
			_coverExtras = _data.GetCoverExtras();
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Loading customer failed for {CustomerId}", CustomerId);
			Error = "Something went wrong while loading the customer. Please try again.";
			return false;
		}
	}

	// ---------- validation ----------

	private List<string> BindingErrors() =>
		ModelState.Values.SelectMany(v => v.Errors)
				  .Select(e => e.ErrorMessage)
				  .Where(m => !string.IsNullOrWhiteSpace(m))
				  .ToList();

	private List<string> ValidateDecision(int? decisionId, int? reasonId)
	{
		var errors = new List<string>();
		var decision = Decisions.FirstOrDefault(d => d.DecisionId == decisionId);
		if (decision is null)
		{
			errors.Add("Choose a decision.");
			return errors;
		}

		var allowed = Reasons.Where(r => r.DecisionId == decision.DecisionId).ToList();
		if (allowed.Count > 0 && reasonId is null)
			errors.Add($"Choose a reason for {decision.DecisionName}.");
		else if (reasonId is not null && !allowed.Any(r => r.ReasonId == reasonId))
			errors.Add("The chosen reason does not belong to the chosen decision.");
		return errors;
	}

	// DEMO rules until the mentor confirms ranges and units: numbers can't be negative
	private static List<string> ValidateDetails(DecisionInput f)
	{
		var errors = new List<string>();
		if (f.PostponePeriod is < 0 or > 999) errors.Add("Postpone period must be between 0 and 999.");
		CheckAmount(errors, "EMR", f.EMR);
		CheckAmount(errors, "Occupation extra", f.OccupationExtra);
		CheckAmount(errors, "Residential extra", f.ResidentialExtra);
		CheckAmount(errors, "Flat extra", f.FlatExtra);
		CheckAmount(errors, "Age extra", f.AgeExtra);
		if (f.Notes is { Length: > 500 }) errors.Add("Notes can be at most 500 characters.");
		return errors;
	}

	private static void CheckAmount(List<string> errors, string label, decimal? value)
	{
		if (value is null) return;
		if (value < 0) errors.Add($"{label} cannot be negative.");
		else if (value > 9999999.99m) errors.Add($"{label} is too large.");
		else if (decimal.Round(value.Value, 2) != value) errors.Add($"{label} can have at most 2 decimal places.");
	}

	// Anything that doesn't apply to the chosen decision and this cover is thrown away,
	// even if someone edited the form by hand.
	private void KeepOnlyApplicableFields(Cover cover)
	{
		var fields = FieldsFor(Form.DecisionId);
		if (!fields.Contains("PostponePeriod")) Form.PostponePeriod = null;

		var extras = fields.Contains("Extras") ? AllowedExtras(cover) : new HashSet<string>();
		if (!extras.Contains("EMR")) Form.EMR = null;
		if (!extras.Contains("OccupationExtra")) Form.OccupationExtra = null;
		if (!extras.Contains("ResidentialExtra")) Form.ResidentialExtra = null;
		if (!extras.Contains("FlatExtra")) Form.FlatExtra = null;
		if (!extras.Contains("AgeExtra")) Form.AgeExtra = null;
	}

	// ---------- helpers used by the page and its partials ----------

	private HashSet<string> FieldsFor(int? decisionId) =>
		decisionId is not null && _decisionFields.TryGetValue(decisionId.Value.ToString(), out var s)
			? s : new HashSet<string>();

	public bool Shows(int? decisionId, string field) => FieldsFor(decisionId).Contains(field);

	public HashSet<string> AllowedExtras(Cover c)
	{
		var kind = c.CoverType == "BASE" ? "BASE" : c.RiderType ?? "";
		return _coverExtras.TryGetValue(kind, out var s) ? s : new HashSet<string>();
	}

	public int CoverCount(string coverType) => Covers.Count(c => c.CoverType == coverType);

	// covers that follow the global decision (they have no decision of their own)
	public List<string> AffectedCovers(string coverType) =>
		Covers.Where(c => c.CoverType == coverType && c.DecisionSource != "OVERRIDE")
			  .Select(c => c.RiderId ?? c.PolicyNumber).ToList();

	// covers with their own decision: a global decision leaves them alone
	public List<string> KeptCovers(string coverType) =>
		Covers.Where(c => c.CoverType == coverType && c.DecisionSource == "OVERRIDE")
			  .Select(c => c.RiderId ?? c.PolicyNumber).ToList();

	public string CurrentGlobalLabel(string coverType)
	{
		var g = Covers.FirstOrDefault(c => c.CoverType == coverType && c.DecisionSource == "GLOBAL");
		if (g is null) return "not set";
		return g.ReasonText is null || g.ReasonText == g.DecisionName
			? g.DecisionName ?? "?"
			: $"{g.DecisionName} ({g.ReasonText})";
	}

	// what the global card's fields show: the saved global decision, or what the user just typed if it failed
	public DecisionInput FormForGlobal(string coverType)
	{
		if (IsPost && Form.UwId is null && Form.CoverType == coverType) return Form;
		var g = Covers.FirstOrDefault(c => c.CoverType == coverType && c.DecisionSource == "GLOBAL");
		return new DecisionInput { CoverType = coverType, DecisionId = g?.DecisionId, ReasonId = g?.ReasonId };
	}

	// what a cover editor's fields show: the saved values, or what the user just typed if its save failed
	public DecisionInput FormForCover(Cover c)
	{
		if (IsPost && Form.UwId == c.UwId) return Form;
		return new DecisionInput
		{
			UwId = c.UwId,
			DecisionId = c.DecisionId,
			ReasonId = c.ReasonId,
			PostponePeriod = c.PostponePeriod,
			EMR = c.EMR,
			OccupationExtra = c.OccupationExtra,
			ResidentialExtra = c.ResidentialExtra,
			FlatExtra = c.FlatExtra,
			AgeExtra = c.AgeExtra,
			Notes = c.Notes
		};
	}

	public string SourceBadge(string? source) => source switch
	{
		"OVERRIDE" => "bg-warning text-dark",
		"GLOBAL" => "bg-secondary",
		_ => "bg-light text-dark border"
	};

	public string? Dec(decimal? v) => v?.ToString("0.##", CultureInfo.InvariantCulture);

	private static string CoverLabel(Cover c) =>
		c.CoverType == "BASE" ? $"base cover of policy {c.PolicyNumber}" : $"rider {c.RiderId}";
}