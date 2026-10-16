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

	// Our text columns are VARCHAR, so we send text parameters as plain (non-Unicode) text.
	// That lets SQL Server use its indexes.
	private static DbString Id(string value) => new() { Value = value, IsAnsi = true, Length = 20 };
	private static DbString Code(string value, int length) => new() { Value = value, IsAnsi = true, Length = length };

	// ---------- reading ----------

	public int CountCustomers()
	{
		using var conn = new SqlConnection(_connectionString);
		return conn.ExecuteScalar<int>("SELECT COUNT(*) FROM Customer");
	}

	public Customer? GetCustomer(string customerId)
	{
		using var conn = new SqlConnection(_connectionString);
		return conn.QuerySingleOrDefault<Customer>(
			"SELECT CustomerId, CustomerName, BenefitGroupId, ReinsurerId FROM Customer WHERE CustomerId = @CustomerId",
			new { CustomerId = Id(customerId) });
	}

	// every cover (base covers and riders) of one customer, with the decision names filled in
	public List<Cover> GetCovers(string customerId)
	{
		using var conn = new SqlConnection(_connectionString);
		return conn.Query<Cover>("""
            SELECT u.UwId, u.CustomerId, u.CoverType, u.PolicyNumber, u.RiderId, u.RiderType,
                   u.MarketName, u.BillingFrequency, u.BenefitCode, u.BenefitGroup,
                   u.Bterm, u.Bppt, u.BenefitInstalmentPremium, u.BenefitSumAssured,
                   u.DecisionId, d.DecisionName, u.ReasonId, r.ReasonText, u.DecisionSource,
                   u.PostponePeriod, u.EMR, u.OccupationExtra, u.ResidentialExtra, u.FlatExtra, u.AgeExtra, u.Notes
            FROM UWDecision u
            LEFT JOIN DecisionOption d ON d.DecisionId = u.DecisionId
            LEFT JOIN DecisionReason r ON r.ReasonId   = u.ReasonId
            WHERE u.CustomerId = @CustomerId
            ORDER BY u.PolicyNumber, CASE WHEN u.CoverType = 'BASE' THEN 0 ELSE 1 END, u.RiderId
            """,
			new { CustomerId = Id(customerId) }).ToList();
	}

	public List<DecisionOption> GetDecisionOptions()
	{
		using var conn = new SqlConnection(_connectionString);
		return conn.Query<DecisionOption>(
			"SELECT DecisionId, DecisionName FROM DecisionOption ORDER BY DecisionId").ToList();
	}

	public List<DecisionReason> GetReasons()
	{
		using var conn = new SqlConnection(_connectionString);
		return conn.Query<DecisionReason>(
			"SELECT ReasonId, DecisionId, ReasonText FROM DecisionReason ORDER BY DecisionId, ReasonId").ToList();
	}

	// decision id (as text) -> the detail fields that appear for it
	public Dictionary<string, HashSet<string>> GetDecisionFields() =>
		ReadRules("SELECT CAST(DecisionId AS VARCHAR(20)) AS RuleKey, FieldName FROM DecisionField");

	// cover kind ('BASE' or a rider type) -> the extras that apply to it
	public Dictionary<string, HashSet<string>> GetCoverExtras() =>
		ReadRules("SELECT CoverKind AS RuleKey, FieldName FROM CoverExtra");

	private Dictionary<string, HashSet<string>> ReadRules(string sql)
	{
		using var conn = new SqlConnection(_connectionString);
		return conn.Query<FieldRule>(sql)
				   .GroupBy(r => r.RuleKey)
				   .ToDictionary(g => g.Key, g => g.Select(r => r.FieldName).ToHashSet());
	}

	// ---------- writing (each method returns the number of rows the database changed) ----------

	// Global decision: one UPDATE for all of the customer's covers of this type, EXCEPT overrides.
	// The CASE lines clear the old decision's details (postpone period, extras) when the decision changes.
	public int SaveGlobalDecision(string customerId, string coverType, int decisionId, int? reasonId)
	{
		using var conn = new SqlConnection(_connectionString);
		return conn.Execute("""
            UPDATE UWDecision
               SET PostponePeriod   = CASE WHEN DecisionId = @DecisionId THEN PostponePeriod   END,
                   EMR              = CASE WHEN DecisionId = @DecisionId THEN EMR              END,
                   OccupationExtra  = CASE WHEN DecisionId = @DecisionId THEN OccupationExtra  END,
                   ResidentialExtra = CASE WHEN DecisionId = @DecisionId THEN ResidentialExtra END,
                   FlatExtra        = CASE WHEN DecisionId = @DecisionId THEN FlatExtra        END,
                   AgeExtra         = CASE WHEN DecisionId = @DecisionId THEN AgeExtra         END,
                   DecisionId = @DecisionId, ReasonId = @ReasonId, DecisionSource = 'GLOBAL'
             WHERE CustomerId = @CustomerId AND CoverType = @CoverType
               AND (DecisionSource IS NULL OR DecisionSource = 'GLOBAL')
            """,
			new
			{
				CustomerId = Id(customerId),
				CoverType = Code(coverType, 5),
				DecisionId = decisionId,
				ReasonId = reasonId
			});
	}

	// One cover saved by hand. The source becomes OVERRIDE only if the decision or reason changed.
	public int SaveCoverDecision(int uwId, string customerId, DecisionInput f)
	{
		using var conn = new SqlConnection(_connectionString);
		return conn.Execute("""
            UPDATE UWDecision
               SET DecisionSource = CASE WHEN DecisionId = @DecisionId
                                          AND ISNULL(ReasonId, 0) = ISNULL(@ReasonId, 0)
                                         THEN DecisionSource ELSE 'OVERRIDE' END,
                   DecisionId = @DecisionId, ReasonId = @ReasonId,
                   PostponePeriod = @PostponePeriod, EMR = @EMR, OccupationExtra = @OccupationExtra,
                   ResidentialExtra = @ResidentialExtra, FlatExtra = @FlatExtra, AgeExtra = @AgeExtra,
                   Notes = @Notes
             WHERE UwId = @UwId AND CustomerId = @CustomerId
            """,
			new
			{
				UwId = uwId,
				CustomerId = Id(customerId),
				f.DecisionId,
				f.ReasonId,
				f.PostponePeriod,
				f.EMR,
				f.OccupationExtra,
				f.ResidentialExtra,
				f.FlatExtra,
				f.AgeExtra,
				f.Notes
			});
	}

	// Reset: copy the customer's global decision onto an overridden cover.
	// If the customer has no global decision, no row matches and 0 is returned.
	public int ResetToGlobal(int uwId, string customerId)
	{
		using var conn = new SqlConnection(_connectionString);
		return conn.Execute("""
            UPDATE u
               SET u.PostponePeriod   = CASE WHEN u.DecisionId = g.DecisionId THEN u.PostponePeriod   END,
                   u.EMR              = CASE WHEN u.DecisionId = g.DecisionId THEN u.EMR              END,
                   u.OccupationExtra  = CASE WHEN u.DecisionId = g.DecisionId THEN u.OccupationExtra  END,
                   u.ResidentialExtra = CASE WHEN u.DecisionId = g.DecisionId THEN u.ResidentialExtra END,
                   u.FlatExtra        = CASE WHEN u.DecisionId = g.DecisionId THEN u.FlatExtra        END,
                   u.AgeExtra         = CASE WHEN u.DecisionId = g.DecisionId THEN u.AgeExtra         END,
                   u.DecisionId = g.DecisionId, u.ReasonId = g.ReasonId, u.DecisionSource = 'GLOBAL'
              FROM UWDecision u
             CROSS APPLY (SELECT TOP 1 x.DecisionId, x.ReasonId
                            FROM UWDecision x
                           WHERE x.CustomerId = u.CustomerId AND x.CoverType = u.CoverType
                             AND x.DecisionSource = 'GLOBAL'
                           ORDER BY x.UwId) g
             WHERE u.UwId = @UwId AND u.CustomerId = @CustomerId AND u.DecisionSource = 'OVERRIDE'
            """,
			new { UwId = uwId, CustomerId = Id(customerId) });
	}
}