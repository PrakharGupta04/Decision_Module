using System.Globalization;
using Decision_Module.Data;
using Decision_Module.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Decision_Module.Pages.Underwriting;

// A global decision row: 'BASE' or a rider type, with the label shown on the page
public record Kind(string Code, string Label);

public class IndexModel : PageModel
{
    private readonly UnderwritingData _data;
    private readonly ILogger<IndexModel> _logger;

    private List<DecisionReason> _reasons = new();
    private List<SubReason> _subReasons = new();
    private Dictionary<string, HashSet<string>> _decisionFields = new();
    private Dictionary<string, HashSet<string>> _coverExtras = new();

    public IndexModel(UnderwritingData data, ILogger<IndexModel> logger)
    {
        _data = data;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string? CustomerId { get; set; }

    [BindProperty]
    public List<GlobalInput> GlobalInputs { get; set; } = new();

    [BindProperty]
    public List<CoverInput> CoverInputs { get; set; } = new();

    [TempData]
    public string? Success { get; set; }

    public Customer? Customer { get; private set; }
    public List<Cover> Covers { get; private set; } = new();
    public List<Cover> Policies { get; private set; } = new();
    public List<Kind> GlobalKinds { get; private set; } = new();
    public List<DecisionOption> Decisions { get; private set; } = new();
    public string? Warning { get; private set; }
    public string? Error { get; private set; }

    public (string Name, string Label)[] ExtraFields { get; } = new[]
    {
        ("EMR", "EMR % (100 = standard)"),
        ("OccupationExtra", "Occupation extra (per 1,000)"),
        ("ResidentialExtra", "Residential extra"),
        ("FlatExtra", "Flat extra (amount)"),
        ("AgeExtra", "Age extra")
    };

    public void OnGet()
    {
        if (!Request.Query.ContainsKey("CustomerId")) return;
        if (Load()) FillInputsFromDatabase();
    }

    public IActionResult OnPost()
    {
        if (!Load()) return Page();

        if (!InputsMatchPage())
        {
            FillInputsFromDatabase();
            Error = "This page is out of date. Please search for the customer again.";
            return Page();
        }

        CheckGlobals();
        CheckCovers();
        if (!ModelState.IsValid) return Page();

        try
        {
            _data.SaveDecisions(CustomerId!, GlobalInputs, CoverInputs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saving decisions failed for {CustomerId}", CustomerId);
            Error = "The decisions could not be saved. Nothing was changed.";
            return Page();
        }

        Success = "Decisions saved.";
        return RedirectToPage(new { CustomerId });
    }

    // ---------- loading ----------

    private bool Load()
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
            GlobalKinds = BuildKinds();
            Decisions = _data.GetDecisions();
            _reasons = _data.GetReasons();
            _subReasons = _data.GetSubReasons();
            _decisionFields = _data.GetDecisionFields();
            _coverExtras = _data.GetCoverExtras();
            return true;
        }
        catch (Exception ex)
        {
            Customer = null;
            _logger.LogError(ex, "Loading customer failed for {CustomerId}", CustomerId);
            Error = "Something went wrong while loading the customer. Please try again.";
            return false;
        }
    }

    // one global decision for the base cover, and one for each rider type the customer has
    private List<Kind> BuildKinds()
    {
        var kinds = new List<Kind>();
        if (Covers.Any(c => c.CoverType == "BASE"))
            kinds.Add(new Kind("BASE", "Base cover"));

        kinds.AddRange(Covers.Where(c => c.CoverType == "RIDER")
                             .Select(c => new Kind(c.RiderType!, c.RiderTypeName ?? c.RiderType!))
                             .DistinctBy(k => k.Code)
                             .OrderBy(k => k.Label));
        return kinds;
    }

	private void FillInputsFromDatabase()
	{
		var savedGlobals = _data.GetGlobalDecisions(CustomerId!);
		var globalSubs = _data.GetGlobalSubReasons(CustomerId!);
		var coverSubs = _data.GetCoverSubReasons(CustomerId!);

		var globalReasonLinks = _data.GetGlobalReasons(CustomerId!);
		var coverReasonLinks = _data.GetCoverReasons(CustomerId!);

		GlobalInputs = GlobalKinds.Select(kind =>
		{
			var saved = savedGlobals.FirstOrDefault(g => g.CoverKind == kind.Code);

			var selectedReasonIds = saved == null
				? new List<int>()
				: globalReasonLinks
					.Where(r => r.GlobalId == saved.GlobalId)
					.Select(r => r.ReasonId)
					.ToList();

			// Support older saved decisions that have only the legacy ReasonId.
			if (selectedReasonIds.Count == 0 && saved?.ReasonId is int oldReasonId)
				selectedReasonIds.Add(oldReasonId);

			var selectedSubIds = globalSubs
				.Where(s => s.CoverKind == kind.Code)
				.Select(s => s.SubReasonId)
				.ToHashSet();

			return new GlobalInput
			{
				Kind = kind.Code,
				On = saved?.IsOn ?? false,
				DecisionId = saved?.DecisionId,
				ReasonId = selectedReasonIds.FirstOrDefault() is var id && id != 0
					? id : null,
				Notes = saved?.Notes,
				Reasons = selectedReasonIds.Distinct().Select(reasonId =>
					new ReasonInput
					{
						ReasonId = reasonId,
						SubReasonIds = _subReasons
							.Where(s => s.ReasonId == reasonId &&
										selectedSubIds.Contains(s.SubReasonId))
							.Select(s => s.SubReasonId)
							.ToList()
					}).ToList(),
				SubReasonIds = selectedSubIds.ToList()
			};
		}).ToList();

		CoverInputs = Covers.Select(cover =>
		{
			var selectedReasonIds = coverReasonLinks
				.Where(r => r.UwId == cover.UwId)
				.Select(r => r.ReasonId)
				.Distinct()
				.ToList();

			// Backward compatibility with the old single-reason column.
			if (selectedReasonIds.Count == 0 && cover.ReasonId is int oldReasonId)
				selectedReasonIds.Add(oldReasonId);

			var selectedSubIds = coverSubs
				.Where(s => s.UwId == cover.UwId)
				.Select(s => s.SubReasonId)
				.ToHashSet();

			return new CoverInput
			{
				UwId = cover.UwId,
				Source = StartingSource(cover),
				DecisionId = cover.DecisionId,
				ReasonId = selectedReasonIds.FirstOrDefault() is var id && id != 0
					? id : null,
				Reasons = selectedReasonIds.Select(reasonId =>
					new ReasonInput
					{
						ReasonId = reasonId,
						SubReasonIds = _subReasons
							.Where(s => s.ReasonId == reasonId &&
										selectedSubIds.Contains(s.SubReasonId))
							.Select(s => s.SubReasonId)
							.ToList()
					}).ToList(),
				SubReasonIds = selectedSubIds.ToList(),
				PostponePeriod = cover.PostponePeriod,
				EMR = cover.EMR,
				OccupationExtra = cover.OccupationExtra,
				ResidentialExtra = cover.ResidentialExtra,
				FlatExtra = cover.FlatExtra,
				AgeExtra = cover.AgeExtra,
				Notes = cover.Notes
			};
		}).ToList();
	}

	// a cover follows the global decision only if its kind has one switched on
	private string StartingSource(Cover cover)
    {
        var globalIsOn = GlobalInputs.Any(g => g.Kind == KindOf(cover) && g.On);
        return globalIsOn ? cover.DecisionSource ?? "GLOBAL" : "OVERRIDE";
    }

    // the posted lists must describe exactly the covers and global rows shown on the page
    private bool InputsMatchPage() =>
        GlobalInputs.Select(g => g.Kind).SequenceEqual(GlobalKinds.Select(k => k.Code)) &&
        CoverInputs.Select(c => c.UwId).SequenceEqual(Covers.Select(c => c.UwId));

	// ---------- validation ----------


	private void CheckGlobals()
	{
		for (var i = 0; i < GlobalInputs.Count; i++)
		{
			var input = GlobalInputs[i];
			var prefix = $"GlobalInputs[{i}]";

			if (!input.On)
			{
				input.DecisionId = null;
				input.ReasonId = null;
				input.Reasons.Clear();
				input.SubReasonIds.Clear();
				continue;
			}

			if (input.DecisionId is null)
			{
				AddError(
					prefix + ".DecisionId",
					"Choose a decision, or untick this global decision.");
				continue;
			}

			CheckReasons(prefix, input.Kind, input);
		}
	}



	private void CheckCovers()
	{
		for (var i = 0; i < CoverInputs.Count; i++)
		{
			var input = CoverInputs[i];
			var cover = Covers[i];
			var prefix = $"CoverInputs[{i}]";
			var kind = KindOf(cover);
			var globalInput = GlobalInputs.First(g => g.Kind == kind);

			if (input.Source == "GLOBAL")
			{
				if (!globalInput.On)
				{
					AddError(
						prefix + ".Source",
						"The global decision is switched off. Choose Override.");
				}
				else
				{
					input.DecisionId = globalInput.DecisionId;
					input.ReasonId = globalInput.ReasonId;

					input.Reasons = globalInput.Reasons
						.Select(r => new ReasonInput
						{
							ReasonId = r.ReasonId,
							SubReasonIds = r.SubReasonIds.ToList()
						})
						.ToList();

					input.SubReasonIds = globalInput.SubReasonIds.ToList();
				}
			}
			else if (input.Source == "OVERRIDE")
			{
				if (input.DecisionId is null)
				{
					input.ReasonId = null;
					input.Reasons.Clear();
					input.SubReasonIds.Clear();
				}
				else
				{
					CheckReasons(prefix, kind, input);
				}
			}
			else
			{
				AddError(
					prefix + ".Source",
					"Choose Global or Override.");
			}

			KeepApplicableDetails(cover, input);
			CheckDetails(prefix, input);
		}
	}


	// A reason is needed when the catalogue has reasons for this decision and cover kind.
	// If the reason has sub-reasons, at least one must be chosen.

	private void CheckReasons(string prefix, string kind, DecisionInput input)
	{
		if (!Decisions.Any(d => d.DecisionId == input.DecisionId))
		{
			AddError(prefix + ".DecisionId", "Choose a valid decision.");
			return;
		}

		var allowedReasons = ReasonsFor(kind)
			.Where(r => r.DecisionId == input.DecisionId)
			.ToList();

		if (allowedReasons.Count == 0)
		{
			input.Reasons.Clear();
			input.ReasonId = null;
			input.SubReasonIds.Clear();
			return;
		}

		input.Reasons = input.Reasons
			.Where(r => r.ReasonId > 0)
			.GroupBy(r => r.ReasonId)
			.Select(g => g.First())
			.ToList();

		if (input.Reasons.Count == 0)
		{
			AddError(prefix + ".Reasons", "Choose at least one reason.");
			return;
		}

		for (var i = 0; i < input.Reasons.Count; i++)
		{
			var selected = input.Reasons[i];
			var reasonPrefix = $"{prefix}.Reasons[{selected.ReasonId}]";

			if (!allowedReasons.Any(r => r.ReasonId == selected.ReasonId))
			{
				AddError(
					reasonPrefix + ".ReasonId",
					"A selected reason is not valid for this decision and cover type.");
				continue;
			}

			var allowedSubReasons = _subReasons
				.Where(s => s.ReasonId == selected.ReasonId)
				.Select(s => s.SubReasonId)
				.ToHashSet();

			selected.SubReasonIds = selected.SubReasonIds
				.Distinct()
				.ToList();

			if (allowedSubReasons.Count == 0)
			{
				selected.SubReasonIds.Clear();
			}
			else if (selected.SubReasonIds.Count == 0)
			{
				AddError(
					reasonPrefix + ".SubReasonIds",
					"Select at least one sub-reason for this reason.");
			}
			else if (selected.SubReasonIds.Any(id => !allowedSubReasons.Contains(id)))
			{
				AddError(
					reasonPrefix + ".SubReasonIds",
					"A selected sub-reason does not belong to this reason.");
			}
		}

		input.ReasonId = input.Reasons.FirstOrDefault()?.ReasonId;

		input.SubReasonIds = input.Reasons
			.SelectMany(r => r.SubReasonIds)
			.Distinct()
			.ToList();
	}


	// postpone period and extras are kept only where the decision and the cover use them
	private void KeepApplicableDetails(Cover cover, CoverInput input)
    {
        var fields = FieldsFor(input.DecisionId);
        if (!fields.Contains("PostponePeriod")) input.PostponePeriod = null;

        var extras = fields.Contains("Extras") ? AllowedExtras(cover) : new HashSet<string>();
        if (!extras.Contains("EMR")) input.EMR = null;
        if (!extras.Contains("OccupationExtra")) input.OccupationExtra = null;
        if (!extras.Contains("ResidentialExtra")) input.ResidentialExtra = null;
        if (!extras.Contains("FlatExtra")) input.FlatExtra = null;
        if (!extras.Contains("AgeExtra")) input.AgeExtra = null;

        input.Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim();
    }

    // demo rules until the mentor confirms ranges: no negative numbers, and 100 is the lowest EMR
    private void CheckDetails(string prefix, CoverInput input)
    {
        if (input.PostponePeriod is < 0 or > 999)
            AddError(prefix + ".PostponePeriod", "Postpone period must be between 0 and 999.");

        CheckAmount(prefix + ".EMR", "EMR", input.EMR, 100);
        CheckAmount(prefix + ".OccupationExtra", "Occupation extra", input.OccupationExtra);
        CheckAmount(prefix + ".ResidentialExtra", "Residential extra", input.ResidentialExtra);
        CheckAmount(prefix + ".FlatExtra", "Flat extra", input.FlatExtra);
        CheckAmount(prefix + ".AgeExtra", "Age extra", input.AgeExtra);

        if (input.Notes is { Length: > 500 })
            AddError(prefix + ".Notes", "Notes can be at most 500 characters.");
    }

    private void CheckAmount(string key, string label, decimal? value, decimal minimum = 0)
    {
        if (value is null) return;

        if (value < minimum) AddError(key, $"{label} cannot be below {minimum}.");
        else if (value > 9999999.99m) AddError(key, $"{label} is too large.");
        else if (decimal.Round(value.Value, 2) != value) AddError(key, $"{label} can have at most 2 decimal places.");
    }

    private void AddError(string key, string message) => ModelState.AddModelError(key, message);

    // ---------- used by the page ----------

    public string KindOf(Cover cover) => cover.CoverType == "BASE" ? "BASE" : cover.RiderType ?? "";

    public List<Cover> CoversOf(Cover policy) =>
        Covers.Where(c => c.PolicyNumber == policy.PolicyNumber).ToList();

    public IEnumerable<DecisionReason> ReasonsFor(string kind) =>
        _reasons.Where(r => r.Kinds.Contains("," + kind + ","));

    public IEnumerable<SubReason> SubReasonsFor(string kind)
    {
        var reasonIds = ReasonsFor(kind).Select(r => r.ReasonId).ToHashSet();
        return _subReasons.Where(s => reasonIds.Contains(s.ReasonId));
    }

    private HashSet<string> FieldsFor(int? decisionId) =>
        decisionId is not null && _decisionFields.TryGetValue(decisionId.Value.ToString(), out var fields)
            ? fields
            : new HashSet<string>();

    public bool Shows(int? decisionId, string field) => FieldsFor(decisionId).Contains(field);

    public string FieldsCsv(int decisionId) => string.Join(",", FieldsFor(decisionId));

    public HashSet<string> AllowedExtras(Cover cover) =>
        _coverExtras.TryGetValue(KindOf(cover), out var extras) ? extras : new HashSet<string>();

    public decimal? GetExtra(CoverInput input, string name) => name switch
    {
        "EMR" => input.EMR,
        "OccupationExtra" => input.OccupationExtra,
        "ResidentialExtra" => input.ResidentialExtra,
        "FlatExtra" => input.FlatExtra,
        "AgeExtra" => input.AgeExtra,
        _ => null
    };

    public string? Dec(decimal? value) => value?.ToString("0.##", CultureInfo.InvariantCulture);
}