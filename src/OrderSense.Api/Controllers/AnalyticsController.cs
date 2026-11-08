using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderSense.Api.Data;
using OrderSense.Api.Dtos;
namespace OrderSense.Api.Controllers;
using Npgsql;
using NpgsqlTypes;
[ApiController]
[Route("api/analytics")]
[Authorize]
public class AnalyticsController(AppDbContext db) : ControllerBase
{
    private const string Sql = """
        WITH latest AS
        (
            SELECT DISTINCT ON (p.order_id) p.order_id, p.risk_level
            FROM predictions p
            ORDER BY p.order_id, p.created_at DESC, p.id DESC
        ),
        main_item AS
        (
            SELECT DISTINCT ON (i.order_id) i.order_id, i.seller_id, i.product_id
            FROM order_items i
            ORDER BY i.order_id, i.price DESC, i.item_no
        ),
        order_rows AS
        (
            SELECT o.purchased_at,
                   c.state,
                   COALESCE(pr.category_en, pr.category) AS category,
                   mi.seller_id,
                   l.risk_level
            FROM orders o
            JOIN customers c ON c.id = o.customer_id
            LEFT JOIN main_item mi ON mi.order_id = o.id
            LEFT JOIN products pr ON pr.id = mi.product_id
            LEFT JOIN latest l ON l.order_id = o.id
            WHERE o.purchased_at >= {0}
            AND o.purchased_at < {1}
        )
        SELECT __KEY__ AS "Key",
               COUNT(*)::int AS "Orders",
               COUNT(risk_level)::int AS "Predicted",
               (COUNT(*) FILTER (WHERE risk_level = 'Low'))::int AS "Low",
               (COUNT(*) FILTER (WHERE risk_level = 'Medium'))::int AS "Medium",
               (COUNT(*) FILTER (WHERE risk_level = 'High'))::int AS "High"
        FROM order_rows
        GROUP BY 1
        ORDER BY __ORDER__
        __LIMIT__
        """;
    [HttpGet("risk-by-day", Name = "GetRiskByDay")]
    [ProducesResponseType<List<AnalyticsPointDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<List<AnalyticsPointDto>> GetByDay([FromQuery] AnalyticsQuery query, CancellationToken ct)
    {
        return QueryAsync("to_char(purchased_at, 'YYYY-MM-DD')", "1", false, true, query, ct);
    }
    [HttpGet("risk-by-category", Name = "GetRiskByCategory")]
    [ProducesResponseType<List<AnalyticsPointDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<List<AnalyticsPointDto>> GetByCategory([FromQuery] AnalyticsQuery query, CancellationToken ct)
    {
        return QueryAsync("COALESCE(category, 'unknown')", "2 DESC, 1", true, false, query, ct);
    }
    [HttpGet("risk-by-state", Name = "GetRiskByState")]
    [ProducesResponseType<List<AnalyticsPointDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<List<AnalyticsPointDto>> GetByState([FromQuery] AnalyticsQuery query, CancellationToken ct)
    {
        return QueryAsync("state", "2 DESC, 1", false, false, query, ct);
    }
    [HttpGet("risk-by-seller", Name = "GetRiskBySeller")]
    [ProducesResponseType<List<AnalyticsPointDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<List<AnalyticsPointDto>> GetBySeller([FromQuery] AnalyticsQuery query, CancellationToken ct)
    {
        return QueryAsync("COALESCE(seller_id, 'unknown')", "2 DESC, 1", true, false, query, ct);
    }
    private async Task<List<AnalyticsPointDto>> QueryAsync(string keyExpression,
        string orderBy,
        bool limit,
        bool sortByKey,
        AnalyticsQuery query,
        CancellationToken ct)
    {
        DateTime from = new DateTime(1900, 1, 1);
        if (query.From is { } fromDate)
        {
            from = fromDate.ToDateTime(TimeOnly.MinValue);
        }
        DateTime to = new DateTime(2999, 1, 1);
        if (query.To is { } toDate)
        {
            to = toDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
        }
        NpgsqlParameter fromParameter = new NpgsqlParameter("periodFrom", NpgsqlDbType.Timestamp) { Value = from };
        NpgsqlParameter toParameter = new NpgsqlParameter("periodTo", NpgsqlDbType.Timestamp) { Value = to };
        string sql = Sql.Replace("__KEY__", keyExpression).Replace("__ORDER__", orderBy);
        object[] parameters;
        if (limit)
        {
            sql = sql.Replace("__LIMIT__", "LIMIT {2}");
            parameters = [fromParameter, toParameter, query.Top];
        }
        else
        {
            sql = sql.Replace("__LIMIT__", "");
            parameters = [fromParameter, toParameter];
        }
        List<AnalyticsRow> rows = await db.Database.SqlQueryRaw<AnalyticsRow>(sql, parameters).ToListAsync(ct);
        if (sortByKey)
        {
            rows.Sort(CompareByKey);
        }
        else
        {
            rows.Sort(CompareByOrdersThenKey);
        }
        List<AnalyticsPointDto> points = new List<AnalyticsPointDto>();
        foreach (AnalyticsRow row in rows)
        {
            points.Add(CreatePoint(row));
        }
        return points;
    }
    private static int CompareByKey(AnalyticsRow a, AnalyticsRow b)
    {
        return string.CompareOrdinal(a.Key, b.Key);
    }
    private static int CompareByOrdersThenKey(AnalyticsRow a, AnalyticsRow b)
    {
        int byOrders = b.Orders.CompareTo(a.Orders);
        if (byOrders != 0)
        {
            return byOrders;
        }
        return string.CompareOrdinal(a.Key, b.Key);
    }
    private static AnalyticsPointDto CreatePoint(AnalyticsRow row)
    {
        double? share = null;
        if (row.Predicted > 0)
        {
            share = (double)(row.Medium + row.High) / row.Predicted;
        }
        return new AnalyticsPointDto(row.Key, row.Orders, row.Predicted, row.Low, row.Medium, row.High, share);
    }
}