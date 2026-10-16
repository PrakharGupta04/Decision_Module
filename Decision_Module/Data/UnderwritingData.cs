
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
			?? throw new InvalidOperationException(
				"Connection string 'UWDecisionDb' not found.");
	}

	private static DbString Id(string value) =>
		new() { Value = value, IsAnsi = true, Length = 20 };

	public int CountCustomers()
	{
		using var conn = new SqlConnection(_connectionString);

		return conn.ExecuteScalar<int>(
			"SELECT COUNT(*) FROM dbo.Customer");
	}

	public Customer? GetCustomer(string customerId)
	{
		using var conn = new SqlConnection(_connectionString);

		return conn.QuerySingleOrDefault<Customer>(
			"""
            SELECT
                CustomerId,
                CustomerName,
                BenefitGroupId,
                ReinsurerId
            FROM dbo.Customer
            WHERE CustomerId = @CustomerId
            """,
			new { CustomerId = Id(customerId) });
	}

	public List<Policy> GetPolicies(string customerId)
	{
		using var conn = new SqlConnection(_connectionString);

		return conn.Query<Policy>(
			"""
            SELECT
                p.PolicyNumber,
                p.CustomerId,
                p.MarketName,
                p.BillingFrequency,
                p.BenefitCode,
                p.BenefitGroup,
                p.Bterm,
                p.Bppt,
                p.BenefitInstalmentPremium,
                p.BenefitSumAssured,
                COALESCE(d.DecisionName, N'No decision yet')
                    AS EffectiveDecision,
                COALESCE(u.DecisionSource, N'-') AS Source
            FROM dbo.Policy AS p
            OUTER APPLY
            (
                SELECT TOP (1)
                    ud.DecisionId,
                    ud.DecisionSource
                FROM dbo.UWDecision AS ud
                WHERE ud.CustomerId = p.CustomerId
                  AND ud.CoverType = 'BASE'
                  AND
                  (
                      ud.PolicyNumber = p.PolicyNumber
                      OR
                      (
                          ud.PolicyNumber IS NULL
                          AND ud.RiderId IS NULL
                      )
                  )
                ORDER BY
                    CASE
                        WHEN ud.PolicyNumber = p.PolicyNumber
                            THEN 0
                        ELSE 1
                    END
            ) AS u
            LEFT JOIN dbo.DecisionOption AS d
                ON d.DecisionId = u.DecisionId
            WHERE p.CustomerId = @CustomerId
            ORDER BY p.PolicyNumber
            """,
			new { CustomerId = Id(customerId) }).AsList();
	}

	public List<Rider> GetRiders(string customerId)
	{
		using var conn = new SqlConnection(_connectionString);

		return conn.Query<Rider>(
			"""
            SELECT
                r.RiderId,
                r.PolicyNumber,
                r.RiderType,
                r.RiderSumAssured,
                r.RiderPremium,
                r.RiderTerm,
                r.RiderPpt,
                COALESCE(d.DecisionName, N'No decision yet')
                    AS EffectiveDecision,
                COALESCE(u.DecisionSource, N'-') AS Source
            FROM dbo.Rider AS r
            INNER JOIN dbo.Policy AS p
                ON p.PolicyNumber = r.PolicyNumber
            OUTER APPLY
            (
                SELECT TOP (1)
                    ud.DecisionId,
                    ud.DecisionSource
                FROM dbo.UWDecision AS ud
                WHERE ud.CustomerId = p.CustomerId
                  AND ud.CoverType = 'RIDER'
                  AND
                  (
                      ud.RiderId = r.RiderId
                      OR
                      (
                          ud.PolicyNumber IS NULL
                          AND ud.RiderId IS NULL
                      )
                  )
                ORDER BY
                    CASE
                        WHEN ud.RiderId = r.RiderId
                            THEN 0
                        ELSE 1
                    END
            ) AS u
            LEFT JOIN dbo.DecisionOption AS d
                ON d.DecisionId = u.DecisionId
            WHERE p.CustomerId = @CustomerId
            ORDER BY p.PolicyNumber, r.RiderId
            """,
			new { CustomerId = Id(customerId) }).AsList();
	}
}
