using Dapper;
using Decision_Module.Models;
using Microsoft.Data.SqlClient;

namespace Decision_Module.Data;

public class UnderwritingData
{
	private readonly string _connectionString;

	public UnderwritingData(IConfiguration config)
	{
		_connectionString = config.GetConnectionString("UWDecisionDb")
			?? throw new InvalidOperationException("Connection string 'UWDecisionDb' not found.");
	}

	// Our ID columns are VARCHAR (plain text), so we send IDs as plain text too.
	// This keeps SQL Server able to use its index, which matters on big tables.
	private static DbString Id(string value) => new() { Value = value, IsAnsi = true, Length = 20 };

	public int CountCustomers()
	{
		using var conn = new SqlConnection(_connectionString);
		return conn.ExecuteScalar<int>("SELECT COUNT(*) FROM Customer");
	}

	public Customer? GetCustomer(string customerId)
	{
		using var conn = new SqlConnection(_connectionString);
		return conn.QuerySingleOrDefault<Customer>(
			"SELECT CustomerId, CustomerName, BenefitGroupId, ReinsurerId " +
			"FROM Customer WHERE CustomerId = @CustomerId",
			new { CustomerId = Id(customerId) });
	}

	public List<Policy> GetPolicies(string customerId)
	{
		using var conn = new SqlConnection(_connectionString);
		return conn.Query<Policy>("""
            SELECT p.PolicyNumber, p.CustomerId, p.MarketName, p.BillingFrequency, p.BenefitCode,
                   p.BenefitGroup, p.Bterm, p.Bppt, p.BenefitInstalmentPremium, p.BenefitSumAssured,
                   v.EffectiveDecision, v.Source
            FROM Policy p
            LEFT JOIN vCoverDecision v ON v.CoverType = 'BASE' AND v.CoverId = p.PolicyNumber
            WHERE p.CustomerId = @CustomerId
            ORDER BY p.PolicyNumber
            """,
			new { CustomerId = Id(customerId) }).ToList();
	}

	public List<Rider> GetRiders(string customerId)
	{
		using var conn = new SqlConnection(_connectionString);
		return conn.Query<Rider>("""
            SELECT r.RiderId, r.PolicyNumber, r.RiderType, r.RiderSumAssured, r.RiderPremium,
                   r.RiderTerm, r.RiderPpt, v.EffectiveDecision, v.Source
            FROM Rider r
            JOIN Policy p ON p.PolicyNumber = r.PolicyNumber
            LEFT JOIN vCoverDecision v ON v.CoverType = 'RIDER' AND v.CoverId = r.RiderId
            WHERE p.CustomerId = @CustomerId
            ORDER BY r.RiderId
            """,
			new { CustomerId = Id(customerId) }).ToList();
	}
}