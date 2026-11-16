using Microsoft.EntityFrameworkCore;
using OrderSense.Api.Data;
using OrderSense.Api.Data.Entities;
using OrderSense.Api.Dtos;

namespace OrderSense.Api.Predictions;

public static class BatchSummary
{
    public const int MaxFactors = 5;

    private const string RowsCte = """
        WITH rows AS
        (
            SELECT r.probability,
                   r.risk_level,
                   r.probability IS NOT NULL AND r.risk_level IS NOT NULL AS predicted,
                   COALESCE(c.category_en, r.input->>'MainCategory', '') AS category,
                   COALESCE(r.input->>'CustomerState', '') AS customer_state,
                   COALESCE(r.input->>'PaymentType', '') AS payment_type
            FROM batch_results r
            LEFT JOIN
            (
                SELECT DISTINCT ON (p.category) p.category, p.category_en
                FROM products p
                WHERE p.category IS NOT NULL AND p.category_en IS NOT NULL
                ORDER BY p.category, p.category_en
            ) c ON c.category = r.input->>'MainCategory'
            WHERE r.job_id = {0}
        )
        """;

    private const string TotalsSql = RowsCte + """
        SELECT COUNT(*)::int AS "Total",
               (COUNT(*) FILTER (WHERE predicted))::int AS "Predicted",
               (COUNT(*) FILTER (WHERE predicted AND risk_level = 'Low'))::int AS "Low",
               (COUNT(*) FILTER (WHERE predicted AND risk_level = 'Medium'))::int AS "Medium",
               (COUNT(*) FILTER (WHERE predicted AND risk_level = 'High'))::int AS "High",
               AVG(probability) FILTER (WHERE predicted) AS "Average"
        FROM rows
        """;

    private const string GroupSql = RowsCte + """
        SELECT __KEY__ AS "Key",
               COUNT(*)::int AS "Orders",
               (COUNT(*) FILTER (WHERE predicted))::int AS "Predicted",
               (COUNT(*) FILTER (WHERE predicted AND risk_level = 'Low'))::int AS "Low",
               (COUNT(*) FILTER (WHERE predicted AND risk_level = 'Medium'))::int AS "Medium",
               (COUNT(*) FILTER (WHERE predicted AND risk_level = 'High'))::int AS "High"
        FROM rows
        GROUP BY __KEY__
        ORDER BY COUNT(*) DESC, __KEY__ COLLATE "C"
        LIMIT {1}
        """;

    private const string FactorsSql = """
        SELECT f.feature AS "Feature", COUNT(*)::int AS "Rows"
        FROM batch_results r
        CROSS JOIN LATERAL
        (
            SELECT e->>'Feature' AS feature
            FROM jsonb_array_elements(CASE WHEN jsonb_typeof(r.factors) = 'array' THEN r.factors ELSE '[]'::jsonb END) e
            WHERE (e->>'Contribution')::double precision > 0
            ORDER BY (e->>'Contribution')::double precision DESC
            LIMIT 1
        ) f
        WHERE r.job_id = {0}
          AND r.probability IS NOT NULL
          AND r.risk_level IN ('Medium', 'High')
        GROUP BY f.feature
        ORDER BY COUNT(*) DESC, f.feature COLLATE "C"
        LIMIT {1}
        """;

    public static async Task<BatchJobSummaryDto> BuildAsync(AppDbContext db, BatchJob job, int top, CancellationToken ct)
    {
        BatchTotalsRow totals = (await db.Database.SqlQueryRaw<BatchTotalsRow>(TotalsSql, job.Id).ToListAsync(ct)).Single();
        List<AnalyticsPointDto> byCategory = await GroupAsync(db, job.Id, "category", top, ct);
        List<AnalyticsPointDto> byState = await GroupAsync(db, job.Id, "customer_state", top, ct);
        List<AnalyticsPointDto> byPayment = await GroupAsync(db, job.Id, "payment_type", top, ct);
        List<BatchFactorRow> factors = await db.Database.SqlQueryRaw<BatchFactorRow>(FactorsSql, job.Id, MaxFactors).ToListAsync(ct);

        return new BatchJobSummaryDto(
            job.Id,
            job.FileName,
            totals.Total,
            totals.Predicted,
            totals.Low,
            totals.Medium,
            totals.High,
            totals.Average,
            byCategory,
            byState,
            byPayment,
            factors.Select(f => new BatchFactorCountDto(f.Feature, RiskFactorMapping.Label(f.Feature), f.Rows)).ToList());
    }

    private static async Task<List<AnalyticsPointDto>> GroupAsync(AppDbContext db, Guid jobId, string keyColumn, int top, CancellationToken ct)
    {
        List<AnalyticsRow> rows = await db.Database
            .SqlQueryRaw<AnalyticsRow>(GroupSql.Replace("__KEY__", keyColumn, StringComparison.Ordinal), jobId, top)
            .ToListAsync(ct);

        return rows
            .Select(r => new AnalyticsPointDto(r.Key, r.Orders, r.Predicted, r.Low, r.Medium, r.High, r.Predicted > 0 ? (double)(r.Medium + r.High) / r.Predicted : null))
            .ToList();
    }
}

public sealed class BatchTotalsRow
{
    public int Total { get; set; }

    public int Predicted { get; set; }

    public int Low { get; set; }

    public int Medium { get; set; }

    public int High { get; set; }

    public double? Average { get; set; }
}

public sealed class BatchFactorRow
{
    public string Feature { get; set; } = string.Empty;

    public int Rows { get; set; }
}
