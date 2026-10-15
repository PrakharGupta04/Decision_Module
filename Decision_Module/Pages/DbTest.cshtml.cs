using Decision_Module.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Decision_Module.Pages;

public class DbTestModel : PageModel
{
	private readonly UnderwritingData _data;

	public DbTestModel(UnderwritingData data) => _data = data;

	public int CustomerCount { get; private set; }
	public string? Error { get; private set; }

	public void OnGet()
	{
		try { CustomerCount = _data.CountCustomers(); }
		catch (Exception ex) { Error = ex.Message; }
	}
}