using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderSense.Api.Data;
using OrderSense.Api.Data.Entities;
using OrderSense.Api.Dtos;

namespace OrderSense.Api.Controllers;

[ApiController]
[Route("/api/orders")]
[Authorize]
public class OrdersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<OrderListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<PagedResponse<OrderListItemDto>> GetAll([FromQuery] OrderQuery query, CancellationToken ct)
    {
        IQueryable<Order> orders = db.Orders.AsNoTracking();

        if (query.Status is not null)
        {
            orders = orders.Where(o => o.Status == query.Status);
        }

        if (query.From is { } from)
        {
            var fromDate = from.ToDateTime(TimeOnly.MinValue);
            orders = orders.Where(o => o.PurchasedAt >= fromDate);
        }

        if (query.To is { } to)
        {
            var toDate = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
            orders = orders.Where(o => o.PurchasedAt < toDate);
        }

        if (query.CustomerState is not null)
        {
            orders = orders.Where(o => o.Customer.State == query.CustomerState);
        }

        if (query.RiskLevel is { } risk)
        {
            orders = orders.Where(o => o.Predictions
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => (RiskLevel?)p.RiskLevel)
                .FirstOrDefault() == risk);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            orders = orders.Where(o => o.Id.StartsWith(query.Search));
        }

        var total = await orders.CountAsync(ct);

        orders = query.Sort == OrderSort.DateAsc
            ? orders.OrderBy(o => o.PurchasedAt).ThenBy(o => o.Id)
            : orders.OrderByDescending(o => o.PurchasedAt).ThenBy(o => o.Id);
        
        var items = await orders
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(o => new OrderListItemDto(
                o.Id,
                o.PurchasedAt,
                o.Status,
                o.Customer.State,
                o.Items.Count,
                o.Items.Sum(i => i.Price + i.FreightValue),
                o.Predictions.OrderByDescending(p => p.CreatedAt).Select(p => (float?)p.Probability).FirstOrDefault(),
                o.Predictions.OrderByDescending(p => p.CreatedAt).Select(p => (RiskLevel?)p.RiskLevel).FirstOrDefault()))
            .ToListAsync(ct);

        return new PagedResponse<OrderListItemDto>(items, query.Page, query.PageSize, total);
    }
}