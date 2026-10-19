using System.Globalization;
using Decision_Module.Data;
using Decision_Module.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Decision_Module.Pages.Underwriting;

public class IndexModel : PageModel
{
    private readonly UnderwritingData _data;
    private readonly ILogger<IndexModel> _logger;

    private Dictionary<string, HashSet<string>> _decisionFields = new();
    private Dictionary<string, HashSet<string>> _coverExtras = new();
    private List<GlobalDecision> _globals = new();

    public IndexModel(UnderwritingData data, ILogger<IndexModel> logger)
    {
        _data = data;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string? CustomerId { get; set; }

    [BindProperty]
    public DecisionInput Form { get; set; } = new();

    [TempData]
    public string? Success { get; set; }

    public Customer? Customer { get; private set; }
    public List<Cover> Covers { get; private set; } = new();
    public List<Cover> Policies { get; private set; } = new();
    public ILookup<string, Cover> RidersByPolicy { get; private set; } =
        Enumerable.Empty<Cover>().ToLookup(c => c.PolicyNumber);
    public List<DecisionOption> Decisions { get; private set; } = new();
    public List<string> Messages { get; private set; } = new();
    public string? Warning { get; private set; }
    public string? Error { get; private set; }
    public int? OpenUwId { get; private set; }
    public string? OpenPolicy { get; private set; }

    private bool IsPost => HttpMethods.IsPost(Request.Method);

    public void OnGet()
    {
        if (!Request.Query.ContainsKey("CustomerId")) return;
        TryLoadCustomer();
    }

    public IActionResult OnPostSaveGlobal()
    {
        if (!TryLoadCustomer()) return Page();

        var coverType = Form.CoverType ?? "";
        if (coverType != "BASE" && coverType != "RIDER")
        {
            Error = "Unknown cover type.";
            return Page();
        }
        if (CoverCount(coverType) == 0)
        {
            Messages.Add("This customer has no covers of this type.");
            return Page();
        }

        Form.ReasonText = Clean(Form.ReasonText);

        var errors = ModelErrors();
        errors.AddRange(CheckDecision(Form));
        if (errors.Count > 0)
        {
            Messages = errors;
            return Page();
        }

        int changed;
        try
        {
            changed = _data.SaveGlobalDecision(CustomerId!, coverType, Form.DecisionId!.Value, Form.ReasonText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saving global decision failed for {CustomerId}", CustomerId);
            Error = "The decision could not be saved. Nothing was changed.";
            return Page();
        }

        var decisionName = Decisions.First(d => d.DecisionId == Form.DecisionId).DecisionName;
        var title = coverType == "BASE" ? "Base cover" : "Riders";
        var kept = KeptCovers(coverType).Count;

        Success = changed == 0
            ? $"{title} global decision saved: {decisionName}. No cover changed, because every cover has its own decision."
            : $"{title} global decision saved: {decisionName}. Applied to {changed} cover(s); {kept} kept (own decision).";

        return RedirectToPage(new { CustomerId });
    }

    public IActionResult OnPostSaveCover()
    {
        if (!TryLoadCustomer()) return Page();

        var cover = Covers.FirstOrDefault(c => c.UwId == Form.UwId);
        if (cover is null)
        {
            Error = "That cover was not found for this customer.";
            return Page();
        }

        OpenUwId = cover.UwId;
        OpenPolicy = cover.PolicyNumber;

        Form.ReasonText = Clean(Form.ReasonText);
        Form.Notes = Clean(Form.Notes);
        KeepOnlyApplicableFields(cover);

        var errors = ModelErrors();
        errors.AddRange(CheckDecision(Form));
        errors.AddRange(CheckDetails(Form));
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

    public IActionResult OnPostResetCover()
    {
        if (!TryLoadCustomer()) return Page();

        var cover = Covers.FirstOrDefault(c => c.UwId == Form.UwId);
        if (cover is null)
        {
            Error = "That cover was not found for this customer.";
            return Page();
        }

        Form = new DecisionInput();
        OpenUwId = cover.UwId;
        OpenPolicy = cover.PolicyNumber;

        if (cover.DecisionSource != "OVERRIDE")
        {
            Messages.Add("This cover already follows the global decision.");
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
            Messages.Add("There is no global decision to return to. Set the global decision for this cover type first.");
            return Page();
        }

        Success = $"Reset: {CoverLabel(cover)} now follows the global decision.";
        return RedirectToPage(new { CustomerId });
    }

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
            _globals = _data.GetGlobalDecisions(CustomerId);
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

    private List<string> ModelErrors() =>
        ModelState.Values.SelectMany(v => v.Errors)
                  .Select(e => e.ErrorMessage)
                  .Where(m => !string.IsNullOrWhiteSpace(m))
                  .ToList();

    private List<string> CheckDecision(DecisionInput input)
    {
        var errors = new List<string>();

        var decision = Decisions.FirstOrDefault(d => d.DecisionId == input.DecisionId);
        if (decision is null)
        {
            errors.Add("Choose a decision.");
            return errors;
        }

        if (input.ReasonText is null && Shows(decision.DecisionId, "ReasonRequired"))
            errors.Add($"Enter a reason for {decision.DecisionName}.");

        if (input.ReasonText is { Length: > 500 })
            errors.Add("The reason can be at most 500 characters.");

        return errors;
    }

    // demo rules until the mentor confirms ranges and units: numbers cannot be negative
    private static List<string> CheckDetails(DecisionInput input)
    {
        var errors = new List<string>();

        if (input.PostponePeriod is < 0 or > 999)
            errors.Add("Postpone period must be between 0 and 999.");

        CheckAmount(errors, "EMR", input.EMR);
        CheckAmount(errors, "Occupation extra", input.OccupationExtra);
        CheckAmount(errors, "Residential extra", input.ResidentialExtra);
        CheckAmount(errors, "Flat extra", input.FlatExtra);
        CheckAmount(errors, "Age extra", input.AgeExtra);

        if (input.Notes is { Length: > 500 })
            errors.Add("Notes can be at most 500 characters.");

        return errors;
    }

    private static void CheckAmount(List<string> errors, string label, decimal? value)
    {
        if (value is null) return;

        if (value < 0) errors.Add($"{label} cannot be negative.");
        else if (value > 9999999.99m) errors.Add($"{label} is too large.");
        else if (decimal.Round(value.Value, 2) != value) errors.Add($"{label} can have at most 2 decimal places.");
    }

    // anything that does not apply to the chosen decision and this cover is dropped, even if the form was edited
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

    private static string? Clean(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static string CoverLabel(Cover cover) =>
        cover.CoverType == "BASE" ? $"base cover of policy {cover.PolicyNumber}" : $"rider {cover.RiderId}";

    // ---- used by the page ----

    private HashSet<string> FieldsFor(int? decisionId) =>
        decisionId is not null && _decisionFields.TryGetValue(decisionId.Value.ToString(), out var fields)
            ? fields
            : new HashSet<string>();

    public bool Shows(int? decisionId, string field) => FieldsFor(decisionId).Contains(field);

    public string FieldsCsv(int decisionId) => string.Join(",", FieldsFor(decisionId));

    public HashSet<string> AllowedExtras(Cover cover)
    {
        var kind = cover.CoverType == "BASE" ? "BASE" : cover.RiderType ?? "";
        return _coverExtras.TryGetValue(kind, out var extras) ? extras : new HashSet<string>();
    }

    public int CoverCount(string coverType) => Covers.Count(c => c.CoverType == coverType);

    // covers that follow the global decision
    public List<string> AffectedCovers(string coverType) =>
        Covers.Where(c => c.CoverType == coverType && c.DecisionSource != "OVERRIDE")
              .Select(c => c.RiderId ?? c.PolicyNumber).ToList();

    // covers with their own decision, which a global decision leaves alone
    public List<string> KeptCovers(string coverType) =>
        Covers.Where(c => c.CoverType == coverType && c.DecisionSource == "OVERRIDE")
              .Select(c => c.RiderId ?? c.PolicyNumber).ToList();

    public string CurrentGlobalLabel(string coverType)
    {
        var global = _globals.FirstOrDefault(g => g.CoverType == coverType);
        if (global is null) return "not set";
        return string.IsNullOrWhiteSpace(global.ReasonText)
            ? global.DecisionName
            : $"{global.DecisionName} ({global.ReasonText})";
    }

    // the global card shows the saved global decision, or what was typed if that save failed
    public DecisionInput FormForGlobal(string coverType)
    {
        if (IsPost && Form.UwId is null && Form.CoverType == coverType) return Form;

        var global = _globals.FirstOrDefault(g => g.CoverType == coverType);
        return new DecisionInput
        {
            CoverType = coverType,
            DecisionId = global?.DecisionId,
            ReasonText = global?.ReasonText
        };
    }

    // a cover's editor shows the saved values, or what was typed if that save failed
    public DecisionInput FormForCover(Cover cover)
    {
        if (IsPost && Form.UwId == cover.UwId) return Form;

        return new DecisionInput
        {
            UwId = cover.UwId,
            DecisionId = cover.DecisionId,
            ReasonText = cover.ReasonText,
            PostponePeriod = cover.PostponePeriod,
            EMR = cover.EMR,
            OccupationExtra = cover.OccupationExtra,
            ResidentialExtra = cover.ResidentialExtra,
            FlatExtra = cover.FlatExtra,
            AgeExtra = cover.AgeExtra,
            Notes = cover.Notes
        };
    }

    public string SourceBadge(string? source) => source switch
    {
        "OVERRIDE" => "bg-warning text-dark",
        "GLOBAL" => "bg-secondary",
        _ => "bg-light text-dark border"
    };

    public string? Dec(decimal? value) => value?.ToString("0.##", CultureInfo.InvariantCulture);
}