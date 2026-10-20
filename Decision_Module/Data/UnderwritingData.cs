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

    // Id columns are VARCHAR, so ids are sent as plain text. That keeps SQL Server's indexes usable.
    private static DbString AnsiText(string? value, int length) =>
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

    // base covers and riders of one customer, ordered policy by policy with the base cover first
    public List<Cover> GetCovers(string customerId)
    {
        using var conn = new SqlConnection(_connectionString);
        return conn.Query<Cover>("""
            SELECT u.UwId, u.CoverType, u.PolicyNumber, u.RiderId, u.RiderType, t.RiderTypeName,
                   u.MarketName, u.BenefitGroup, u.BenefitCode, u.Bterm, u.Bppt,
                   u.BenefitInstalmentPremium, u.BenefitSumAssured,
                   u.PaymentFrequency, f.Label AS FrequencyLabel, u.ReinsurerId, u.BaseCoverId, u.BaseCoverType,
                   u.DecisionSource, u.DecisionId, u.ReasonId, u.PostponePeriod,
                   u.EMR, u.OccupationExtra, u.ResidentialExtra, u.FlatExtra, u.AgeExtra, u.Notes
            FROM UWDecision u
            LEFT JOIN RiderType t ON t.RiderType = u.RiderType
            LEFT JOIN FrequencyCode f ON f.Code = u.PaymentFrequency
            WHERE u.CustomerId = @CustomerId
            ORDER BY u.PolicyNumber, CASE WHEN u.CoverType = 'BASE' THEN 0 ELSE 1 END, u.RiderId
            """,
            new { CustomerId = AnsiText(customerId, 20) }).ToList();
    }

    public List<CoverSubReason> GetCoverSubReasons(string customerId)
    {
        using var conn = new SqlConnection(_connectionString);
        return conn.Query<CoverSubReason>("""
            SELECT s.UwId, s.SubReasonId
            FROM UWDecisionSubReason s
            JOIN UWDecision u ON u.UwId = s.UwId
            WHERE u.CustomerId = @CustomerId
            """,
            new { CustomerId = AnsiText(customerId, 20) }).ToList();
    }

    public List<GlobalDecision> GetGlobalDecisions(string customerId)
    {
        using var conn = new SqlConnection(_connectionString);
        return conn.Query<GlobalDecision>(
            "SELECT GlobalId, CoverKind, IsOn, DecisionId, ReasonId FROM UWGlobalDecision WHERE CustomerId = @CustomerId",
            new { CustomerId = AnsiText(customerId, 20) }).ToList();
    }

    public List<GlobalSubReason> GetGlobalSubReasons(string customerId)
    {
        using var conn = new SqlConnection(_connectionString);
        return conn.Query<GlobalSubReason>("""
            SELECT g.CoverKind, s.SubReasonId
            FROM UWGlobalSubReason s
            JOIN UWGlobalDecision g ON g.GlobalId = s.GlobalId
            WHERE g.CustomerId = @CustomerId
            """,
            new { CustomerId = AnsiText(customerId, 20) }).ToList();
    }

    public List<DecisionOption> GetDecisions()
    {
        using var conn = new SqlConnection(_connectionString);
        return conn.Query<DecisionOption>(
            "SELECT DecisionId, DecisionName FROM DecisionOption ORDER BY DecisionId").ToList();
    }

    public List<DecisionReason> GetReasons()
    {
        using var conn = new SqlConnection(_connectionString);
        return conn.Query<DecisionReason>("""
            SELECT r.ReasonId, r.DecisionId, r.ReasonText, ',' + STRING_AGG(c.CoverKind, ',') + ',' AS Kinds
            FROM DecisionReason r
            JOIN ReasonCover c ON c.ReasonId = r.ReasonId
            GROUP BY r.ReasonId, r.DecisionId, r.ReasonText
            ORDER BY r.DecisionId, r.ReasonId
            """).ToList();
    }

    public List<SubReason> GetSubReasons()
    {
        using var conn = new SqlConnection(_connectionString);
        return conn.Query<SubReason>(
            "SELECT SubReasonId, ReasonId, SubReasonText FROM SubReason ORDER BY ReasonId, SubReasonId").ToList();
    }

    // decision id -> the detail fields that decision uses ('Extras', 'PostponePeriod')
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

    // Saves the whole page in one transaction: if anything fails, nothing is changed.
    public void SaveDecisions(string customerId, List<GlobalInput> globals, List<CoverInput> covers)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var transaction = conn.BeginTransaction();

        foreach (var input in globals)
            SaveGlobal(conn, transaction, customerId, input);

        foreach (var input in covers)
            SaveCover(conn, transaction, customerId, input);

        transaction.Commit();
    }

    private void SaveGlobal(SqlConnection conn, SqlTransaction transaction, string customerId, GlobalInput input)
    {
        var values = new
        {
            CustomerId = AnsiText(customerId, 20),
            Kind = AnsiText(input.Kind, 50),
            IsOn = input.On,
            input.DecisionId,
            input.ReasonId
        };

        conn.Execute("""
            UPDATE UWGlobalDecision
               SET IsOn = @IsOn, DecisionId = @DecisionId, ReasonId = @ReasonId
             WHERE CustomerId = @CustomerId AND CoverKind = @Kind;

            IF @@ROWCOUNT = 0
                INSERT INTO UWGlobalDecision (CustomerId, CoverKind, IsOn, DecisionId, ReasonId)
                VALUES (@CustomerId, @Kind, @IsOn, @DecisionId, @ReasonId);
            """, values, transaction);

        var globalId = conn.ExecuteScalar<int>(
            "SELECT GlobalId FROM UWGlobalDecision WHERE CustomerId = @CustomerId AND CoverKind = @Kind",
            values, transaction);

        conn.Execute("DELETE FROM UWGlobalSubReason WHERE GlobalId = @GlobalId", new { GlobalId = globalId }, transaction);
        conn.Execute("INSERT INTO UWGlobalSubReason (GlobalId, SubReasonId) VALUES (@GlobalId, @SubReasonId)",
            input.SubReasonIds.Select(id => new { GlobalId = globalId, SubReasonId = id }), transaction);
    }

    private void SaveCover(SqlConnection conn, SqlTransaction transaction, string customerId, CoverInput input)
    {
        conn.Execute("""
            UPDATE UWDecision
               SET DecisionSource = @Source, DecisionId = @DecisionId, ReasonId = @ReasonId,
                   PostponePeriod = @PostponePeriod, EMR = @EMR, OccupationExtra = @OccupationExtra,
                   ResidentialExtra = @ResidentialExtra, FlatExtra = @FlatExtra, AgeExtra = @AgeExtra,
                   Notes = @Notes
             WHERE UwId = @UwId AND CustomerId = @CustomerId
            """,
            new
            {
                input.UwId,
                CustomerId = AnsiText(customerId, 20),
                Source = AnsiText(input.Source, 8),
                input.DecisionId,
                input.ReasonId,
                input.PostponePeriod,
                input.EMR,
                input.OccupationExtra,
                input.ResidentialExtra,
                input.FlatExtra,
                input.AgeExtra,
                input.Notes
            }, transaction);

        conn.Execute("DELETE FROM UWDecisionSubReason WHERE UwId = @UwId", new { input.UwId }, transaction);
        conn.Execute("INSERT INTO UWDecisionSubReason (UwId, SubReasonId) VALUES (@UwId, @SubReasonId)",
            input.SubReasonIds.Select(id => new { input.UwId, SubReasonId = id }), transaction);
    }
}