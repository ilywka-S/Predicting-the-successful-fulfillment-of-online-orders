using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderSense.Api.Data;
using OrderSense.Api.Data.Entities;
using OrderSense.Api.Dtos;
namespace OrderSense.Api.Controllers;
[ApiController]
[Route("api/analytics")]
[Authorize]
public class AnalyticsController(AppDbContext db) : ControllerBase
{
    [HttpGet("risk-by-day", Name = "GetRiskByDay")]
    [ProducesResponseType<List<AnalyticsPointDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<List<AnalyticsPointDto>> GetByDay([FromQuery] AnalyticsQuery query, CancellationToken ct)
    {
        List<DayGroup> groups = await Rows(query)
            .GroupBy(r => r.Day)
            .Select(g => new DayGroup
            {
                Key = g.Key,
                Orders = g.Count(),
                Predicted = g.Count(r => r.Risk != null),
                Low = g.Count(r => r.Risk == RiskLevel.Low),
                Medium = g.Count(r => r.Risk == RiskLevel.Medium),
                High = g.Count(r => r.Risk == RiskLevel.High),
            })
            .OrderBy(g => g.Key)
            .ToListAsync(ct);
        List<AnalyticsPointDto> points = new List<AnalyticsPointDto>();
        foreach (DayGroup group in groups)
        {
            string key = group.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            points.Add(CreatePoint(key, group));
        }
        return points;
    }
    [HttpGet("risk-by-category", Name = "GetRiskByCategory")]
    [ProducesResponseType<List<AnalyticsPointDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<List<AnalyticsPointDto>> GetByCategory([FromQuery] AnalyticsQuery query, CancellationToken ct)
    {
        List<TextGroup> groups = await Rows(query)
            .GroupBy(r => r.Category)
            .Select(g => new TextGroup
            {
                Key = g.Key,
                Orders = g.Count(),
                Predicted = g.Count(r => r.Risk != null),
                Low = g.Count(r => r.Risk == RiskLevel.Low),
                Medium = g.Count(r => r.Risk == RiskLevel.Medium),
                High = g.Count(r => r.Risk == RiskLevel.High),
            })
            .OrderByDescending(g => g.Orders)
            .ThenBy(g => g.Key)
            .Take(query.Top)
            .ToListAsync(ct);

        return ToPoints(groups);
    }
    [HttpGet("risk-by-state", Name = "GetRiskByState")]
    [ProducesResponseType<List<AnalyticsPointDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<List<AnalyticsPointDto>> GetByState([FromQuery] AnalyticsQuery query, CancellationToken ct)
    {
        List<TextGroup> groups = await Rows(query)
            .GroupBy(r => r.State)
            .Select(g => new TextGroup
            {
                Key = g.Key,
                Orders = g.Count(),
                Predicted = g.Count(r => r.Risk != null),
                Low = g.Count(r => r.Risk == RiskLevel.Low),
                Medium = g.Count(r => r.Risk == RiskLevel.Medium),
                High = g.Count(r => r.Risk == RiskLevel.High),
            })
            .OrderByDescending(g => g.Orders)
            .ThenBy(g => g.Key)
            .ToListAsync(ct);

        return ToPoints(groups);
    }
    [HttpGet("risk-by-seller", Name = "GetRiskBySeller")]
    [ProducesResponseType<List<AnalyticsPointDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<List<AnalyticsPointDto>> GetBySeller([FromQuery] AnalyticsQuery query, CancellationToken ct)
    {
        List<TextGroup> groups = await Rows(query)
            .GroupBy(r => r.SellerId)
            .Select(g => new TextGroup
            {
                Key = g.Key,
                Orders = g.Count(),
                Predicted = g.Count(r => r.Risk != null),
                Low = g.Count(r => r.Risk == RiskLevel.Low),
                Medium = g.Count(r => r.Risk == RiskLevel.Medium),
                High = g.Count(r => r.Risk == RiskLevel.High),
            })
            .OrderByDescending(g => g.Orders)
            .ThenBy(g => g.Key)
            .Take(query.Top)
            .ToListAsync(ct);

        return ToPoints(groups);
    }
    //oдин рядок на замовлення: ключі групування та рівень ризику останнього прогнозу.
    private IQueryable<OrderRiskRow> Rows(AnalyticsQuery query)
    {
        IQueryable<Order> orders = db.Orders.AsNoTracking();
        if (query.From is { } from)
        {
            DateTime fromDate = from.ToDateTime(TimeOnly.MinValue);
            orders = orders.Where(o => o.PurchasedAt >= fromDate);
        }
        if (query.To is { } to)
        {
            DateTime toDate = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
            orders = orders.Where(o => o.PurchasedAt < toDate);
        }
        return orders.Select(o => new OrderRiskRow
        {
            Day = o.PurchasedAt.Date,
            State = o.Customer.State,
            Category = o.Items
                .OrderByDescending(i => i.Price)
                .ThenBy(i => i.ItemNo)
                .Select(i => i.Product.CategoryEn ?? i.Product.Category)
                .FirstOrDefault(),
            SellerId = o.Items
                .OrderByDescending(i => i.Price)
                .ThenBy(i => i.ItemNo)
                .Select(i => i.SellerId)
                .FirstOrDefault(),
            Risk = o.Predictions
                .OrderByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.Id)
                .Select(p => (RiskLevel?)p.RiskLevel)
                .FirstOrDefault(),
        });
    }
    private static List<AnalyticsPointDto> ToPoints(List<TextGroup> groups)
    {
        List<AnalyticsPointDto> points = new List<AnalyticsPointDto>();
        foreach (TextGroup group in groups)
        {
            string key = group.Key ?? "unknown";
            points.Add(CreatePoint(key, group));
        }
        return points;
    }
    private static AnalyticsPointDto CreatePoint(string key, GroupCounts counts)
    {
        double? share = null;
        if (counts.Predicted > 0)
        {
            share = (double)(counts.Medium + counts.High) / counts.Predicted;
        }
        return new AnalyticsPointDto(key, counts.Orders, counts.Predicted, counts.Low, counts.Medium, counts.High, share);
    }
    private sealed class OrderRiskRow
    {
        public DateTime Day 
        { 
            get; 
            set; 
        }
        public string State 
        { 
            get; 
            set; 
        } = "";
        public string? Category 
        { 
            get; 
            set; 
        }
        public string? SellerId 
        { 
            get; 
            set; 
        }
        public RiskLevel? Risk 
        { 
            get; 
            set; 
        }
    }
    private class GroupCounts
    {
        public int Orders 
        { 
            get; 
            set; 
        }
        public int Predicted 
        { 
            get; 
            set; 
        }
        public int Low 
        { 
            get; 
            set; 
        }
        public int Medium 
        { 
            get; 
            set; 
        }
        public int High 
        { 
            get; 
            set; 
        }
    }
    private sealed class DayGroup : GroupCounts
    {
        public DateTime Key 
        { 
            get; 
            set; 
        }
    }
    private sealed class TextGroup : GroupCounts
    {
        public string? Key 
        { 
            get; 
            set; 
        }
    }
}