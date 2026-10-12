using System.Net;
using System.Net.Http.Json;
using OrderSense.Api.Auth;
using OrderSense.Api.Data.Entities;
using OrderSense.Api.Dtos;
using OrderSense.Api.Tests.Infrastructure;

namespace OrderSense.Api.Tests.Orders;

[Collection(ApiCollection.Name)]
public class OrdersTests(ApiFactory factory)
{
    private static readonly DateTime Day = new(2018, 3, 10, 12, 0, 0);

    [Fact]
    public async Task GetAll_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/orders");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Analyst)]
    [InlineData(Roles.Admin)]
    public async Task GetAll_AnyRole_Returns200(string role)
    {
        var auth = await factory.LoginAsync(await factory.CreateUserAsync(role));

        var response = await factory.CreateClient(auth.AccessToken).GetAsync("/api/orders?pageSize=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_PagesNewestFirst()
    {
        var data = OrderTestData.Create();
        var oldest = data.Order(Day);
        var middle = data.Order(Day.AddDays(1));
        var newest = data.Order(Day.AddDays(2));
        await factory.SeedAsync(oldest, middle, newest);

        var page1 = await GetPageAsync($"search={data.Prefix}&pageSize=2&page=1");
        var page2 = await GetPageAsync($"search={data.Prefix}&pageSize=2&page=2");

        Assert.Equal(3, page1.Total);
        Assert.Equal(new[] { newest.Id, middle.Id }, page1.Items.Select(o => o.Id));
        Assert.Equal(new[] { oldest.Id }, page2.Items.Select(o => o.Id));
    }

    [Fact]
    public async Task GetAll_SortDateAsc_ReturnsOldestFirst()
    {
        var data = OrderTestData.Create();
        var oldest = data.Order(Day);
        var newest = data.Order(Day.AddDays(1));
        await factory.SeedAsync(newest, oldest);

        var page = await GetPageAsync($"search={data.Prefix}&sort=dateAsc");

        Assert.Equal(new[] { oldest.Id, newest.Id }, page.Items.Select(o => o.Id));
    }

    [Fact]
    public async Task GetAll_FiltersByStatus()
    {
        var data = OrderTestData.Create();
        var delivered = data.Order(Day, status: "delivered");
        var canceled = data.Order(Day, status: "canceled");
        await factory.SeedAsync(delivered, canceled);

        var page = await GetPageAsync($"search={data.Prefix}&status=canceled");

        Assert.Equal(new[] { canceled.Id }, page.Items.Select(o => o.Id));
    }

    [Fact]
    public async Task GetAll_FiltersByPurchasePeriod_InclusiveBounds()
    {
        var data = OrderTestData.Create();
        var before = data.Order(new DateTime(2018, 2, 28, 23, 59, 0));
        var firstDay = data.Order(new DateTime(2018, 3, 1, 0, 0, 0));
        var lastDay = data.Order(new DateTime(2018, 3, 31, 23, 59, 0));
        var after = data.Order(new DateTime(2018, 4, 1, 0, 0, 0));
        await factory.SeedAsync(before, firstDay, lastDay, after);

        var page = await GetPageAsync($"search={data.Prefix}&from=2018-03-01&to=2018-03-31&sort=dateAsc");

        Assert.Equal(new[] { firstDay.Id, lastDay.Id }, page.Items.Select(o => o.Id));
    }

    [Fact]
    public async Task GetAll_FiltersByCustomerState()
    {
        var data = OrderTestData.Create();
        var sp = data.Order(Day, state: "SP");
        var rj = data.Order(Day, state: "RJ");
        await factory.SeedAsync(sp, rj);

        var page = await GetPageAsync($"search={data.Prefix}&customerState=RJ");

        Assert.Equal(new[] { rj.Id }, page.Items.Select(o => o.Id));
    }

    [Fact]
    public async Task GetAll_FiltersByRiskOfLatestPrediction()
    {
        var data = OrderTestData.Create();
        var nowHigh = data.Order(Day);
        data.AddPrediction(nowHigh, RiskLevel.Low, DateTimeOffset.UtcNow.AddHours(-1));
        data.AddPrediction(nowHigh, RiskLevel.High, DateTimeOffset.UtcNow);
        var wasHigh = data.Order(Day);
        data.AddPrediction(wasHigh, RiskLevel.High, DateTimeOffset.UtcNow.AddHours(-1));
        data.AddPrediction(wasHigh, RiskLevel.Low, DateTimeOffset.UtcNow);
        await factory.SeedAsync(nowHigh, wasHigh);

        var page = await GetPageAsync($"search={data.Prefix}&riskLevel=high");

        Assert.Equal(new[] { nowHigh.Id }, page.Items.Select(o => o.Id));
    }

    [Fact]
    public async Task GetAll_ReturnsTotalsAndLatestPrediction()
    {
        var data = OrderTestData.Create();
        var order = data.Order(Day);
        data.AddPrediction(order, RiskLevel.Medium, DateTimeOffset.UtcNow);
        await factory.SeedAsync(order);

        var item = Assert.Single((await GetPageAsync($"search={data.Prefix}")).Items);

        Assert.Equal(1, item.ItemsCount);
        Assert.Equal(115m, item.TotalValue);
        Assert.Equal("SP", item.CustomerState);
        Assert.Equal(RiskLevel.Medium, item.RiskLevel);
        Assert.Equal(0.7f, item.Probability);
    }

    [Theory]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("page=0")]
    [InlineData("status=lost")]
    [InlineData("customerState=sp")]
    [InlineData("riskLevel=extreme")]
    [InlineData("from=2018-03-10&to=2018-03-01")]
    public async Task GetAll_InvalidQuery_Returns400(string query)
    {
        var client = await factory.CreateManagerClientAsync();

        var response = await client.GetAsync($"/api/orders?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<PagedResponse<OrderListItemDto>> GetPageAsync(string query)
    {
        var client = await factory.CreateManagerClientAsync();

        return (await client.GetFromJsonAsync<PagedResponse<OrderListItemDto>>($"/api/orders?{query}", ApiJson.Options))!;
    }
}