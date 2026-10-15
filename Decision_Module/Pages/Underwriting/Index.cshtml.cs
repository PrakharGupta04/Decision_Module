using Decision_Module.Data;
using Decision_Module.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Decision_Module.Pages.Underwriting;

public class IndexModel : PageModel
{
	private readonly UnderwritingData _data;
	private readonly ILogger<IndexModel> _logger;

	public IndexModel(UnderwritingData data, ILogger<IndexModel> logger)
	{
		_data = data;
		_logger = logger;
	}

	[BindProperty(SupportsGet = true)]
	public string? CustomerId { get; set; }

	public Customer? Customer { get; private set; }
	public List<Policy> Policies { get; private set; } = new();
	public ILookup<string, Rider> RidersByPolicy { get; private set; } =
		Enumerable.Empty<Rider>().ToLookup(r => r.PolicyNumber);
	public string? Warning { get; private set; }
	public string? Error { get; private set; }

	public void OnGet()
	{
		// First visit: no search yet, just show the empty form
		if (!Request.Query.ContainsKey("CustomerId")) return;

		CustomerId = CustomerId?.Trim();

		if (string.IsNullOrEmpty(CustomerId))
		{
			Warning = "Enter a Customer ID.";
			return;
		}
		if (CustomerId.Length > 20)
		{
			Warning = "Customer ID is too long (maximum 20 characters).";
			return;
		}

		try
		{
			Customer = _data.GetCustomer(CustomerId);
			if (Customer is null)
			{
				Warning = $"No customer found with ID {CustomerId}.";
			}
			else
			{
				Policies = _data.GetPolicies(CustomerId);
				RidersByPolicy = _data.GetRiders(CustomerId).ToLookup(r => r.PolicyNumber);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Lookup failed for {CustomerId}", CustomerId);
			Error = "Something went wrong while loading the customer. Please try again.";
		}
	}
}