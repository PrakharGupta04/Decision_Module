namespace Decision_Module.Models;

public class Customer
{
	public string CustomerId { get; set; } = "";
	public string CustomerName { get; set; } = "";
	public string? BenefitGroupId { get; set; }
	public string? ReinsurerId { get; set; }
}
