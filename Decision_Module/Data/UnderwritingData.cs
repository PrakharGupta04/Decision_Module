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

    // Our id columns are VARCHAR, so ids are sent as plain text. That keeps SQL Server's indexes usable.
    private static DbString AnsiText(string value, int length) =>
        new() { Value = value, IsAnsi = true, Length = length };

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
            new { CustomerId = AnsiText(customerId, 20) });
    }

    public List<Cover> GetCovers(string customerId)
    {
        using var conn = new SqlConnection(_connectionString);
        return conn.Query<Cover>("""
            SELECT u.UwId, u.CustomerId, u.CoverType, u.PolicyNumber, u.RiderId, u.RiderType,
                   u.MarketName, u.BillingFrequency, u.BenefitCode, u.BenefitGroup,
                   u.Bterm, u.Bppt, u.BenefitInstalmentPremium, u.BenefitSumAssured,
                   u.DecisionId, d.DecisionName, u.ReasonText, u.DecisionSource,
                   u.PostponePeriod, u.EMR, u.OccupationExtra, u.ResidentialExtra, u.FlatExtra, u.AgeExtra, u.Notes
            FROM UWDecision u
            LEFT JOIN DecisionOption d ON d.DecisionId = u.DecisionId
            WHERE u.CustomerId = @CustomerId
            ORDER BY u.PolicyNumber, CASE WHEN u.CoverType = 'BASE' THEN 0 ELSE 1 END, u.RiderId
            """,
            new { CustomerId = AnsiText(customerId, 20) }).ToList();
    }

    public List<DecisionOption> GetDecisionOptions()
    {
        using var conn = new SqlConnection(_connectionString);
        return conn.Query<DecisionOption>(
            "SELECT DecisionId, DecisionName FROM DecisionOption ORDER BY DecisionId").ToList();
    }

    public List<GlobalDecision> GetGlobalDecisions(string customerId)
    {
        using var conn = new SqlConnection(_connectionString);
        return conn.Query<GlobalDecision>("""
            SELECT g.CoverType, g.DecisionId, d.DecisionName, g.ReasonText
            FROM UWGlobalDecision g
            JOIN DecisionOption d ON d.DecisionId = g.DecisionId
            WHERE g.CustomerId = @CustomerId
            """,
            new { CustomerId = AnsiText(customerId, 20) }).ToList();
    }

    // decision id -> the extra fields that decision uses (Extras, PostponePeriod, ReasonRequired)
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

    // Saves the customer's global decision, then applies it to every cover of that type that has no
    // decision of its own. Returns how many covers were changed. Both steps succeed or both are undone.
    public int SaveGlobalDecision(string customerId, string coverType, int decisionId, string? reasonText)
    {
        var values = new
        {
            CustomerId = AnsiText(customerId, 20),
            CoverType = AnsiText(coverType, 5),
            DecisionId = decisionId,
            ReasonText = reasonText
        };

        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var transaction = conn.BeginTransaction();

        conn.Execute("""
            UPDATE UWGlobalDecision
               SET DecisionId = @DecisionId, ReasonText = @ReasonText
             WHERE CustomerId = @CustomerId AND CoverType = @CoverType;

            IF @@ROWCOUNT = 0
                INSERT INTO UWGlobalDecision (CustomerId, CoverType, DecisionId, ReasonText)
                VALUES (@CustomerId, @CoverType, @DecisionId, @ReasonText);
            """, values, transaction);

        // Overrides are skipped. Postpone period and extras are cleared only when the decision changes.
        var changed = conn.Execute("""
            UPDATE UWDecision
               SET PostponePeriod   = CASE WHEN DecisionId = @DecisionId THEN PostponePeriod   END,
                   EMR              = CASE WHEN DecisionId = @DecisionId THEN EMR              END,
                   OccupationExtra  = CASE WHEN DecisionId = @DecisionId THEN OccupationExtra  END,
                   ResidentialExtra = CASE WHEN DecisionId = @DecisionId THEN ResidentialExtra END,
                   FlatExtra        = CASE WHEN DecisionId = @DecisionId THEN FlatExtra        END,
                   AgeExtra         = CASE WHEN DecisionId = @DecisionId THEN AgeExtra         END,
                   DecisionId = @DecisionId, ReasonId = NULL, ReasonText = @ReasonText, DecisionSource = 'GLOBAL'
             WHERE CustomerId = @CustomerId AND CoverType = @CoverType
               AND (DecisionSource IS NULL OR DecisionSource = 'GLOBAL')
            """, values, transaction);

        transaction.Commit();
        return changed;
    }

    // Saves one cover by hand. It becomes an override only if the decision or the reason changed.
    public int SaveCoverDecision(int uwId, string customerId, DecisionInput form)
    {
        using var conn = new SqlConnection(_connectionString);
        return conn.Execute("""
            UPDATE UWDecision
               SET DecisionSource = CASE WHEN DecisionId = @DecisionId
                                          AND ISNULL(ReasonText, '') = ISNULL(@ReasonText, '')
                                         THEN DecisionSource ELSE 'OVERRIDE' END,
                   DecisionId = @DecisionId, ReasonId = NULL, ReasonText = @ReasonText,
                   PostponePeriod = @PostponePeriod, EMR = @EMR, OccupationExtra = @OccupationExtra,
                   ResidentialExtra = @ResidentialExtra, FlatExtra = @FlatExtra, AgeExtra = @AgeExtra,
                   Notes = @Notes
             WHERE UwId = @UwId AND CustomerId = @CustomerId
            """,
            new
            {
                UwId = uwId,
                CustomerId = AnsiText(customerId, 20),
                form.DecisionId,
                form.ReasonText,
                form.PostponePeriod,
                form.EMR,
                form.OccupationExtra,
                form.ResidentialExtra,
                form.FlatExtra,
                form.AgeExtra,
                form.Notes
            });
    }

    // Puts an overridden cover back on the customer's global decision. Returns 0 if there is nothing to do.
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
                   u.DecisionId = g.DecisionId, u.ReasonId = NULL, u.ReasonText = g.ReasonText, u.DecisionSource = 'GLOBAL'
              FROM UWDecision u
              JOIN UWGlobalDecision g ON g.CustomerId = u.CustomerId AND g.CoverType = u.CoverType
             WHERE u.UwId = @UwId AND u.CustomerId = @CustomerId AND u.DecisionSource = 'OVERRIDE'
            """,
            new { UwId = uwId, CustomerId = AnsiText(customerId, 20) });
    }
}