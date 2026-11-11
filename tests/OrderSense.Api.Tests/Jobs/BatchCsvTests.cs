using System.Net;
using System.Text;
using OrderSense.Api.Auth;
using OrderSense.Api.Dtos;
using OrderSense.Api.Predictions;
using OrderSense.Api.Tests.Infrastructure;
namespace OrderSense.Api.Tests.Jobs;
public class BatchCsvTests
{
    private const string Header = "itemsCount,totalPrice,freightShare,totalWeightG,promisedDays,distanceKm,customerState,sellerState,mainCategory,paymentType,installments,purchaseDayOfWeek";
    private const string GoodRow = "1,120.50,0.15,800,20,450,SP,RJ,cama_mesa_banho,credit_card,1,2";
    private static BatchCsvParseResult Parse(string text)
    {
        using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        return BatchCsv.Parse(stream);
    }
    [Fact]
    public void Parse_ValidFile_ReturnsRowsWithFeatures()
    {
        BatchCsvParseResult result = Parse($"{Header}\r\n{GoodRow}\r\n{GoodRow}\r\n");
        Assert.Empty(result.FileErrors);
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(1, result.Rows[0].RowNo);
        Assert.Null(result.Rows[0].Error);
        Assert.Equal(120.5f, result.Rows[0].Features!.TotalPrice);
        Assert.Equal("RJ", result.Rows[0].Features!.SellerState);
        Assert.Equal(2, result.Rows[1].RowNo);
    }
    [Fact]
    public void Parse_HeaderWithBomAndOtherCase_IsAccepted()
    {
        string text = "\uFEFF" + Header.ToUpperInvariant() + "\n" + GoodRow + "\n";
        BatchCsvParseResult result = Parse(text);
        Assert.Empty(result.FileErrors);
        Assert.Single(result.Rows);
        Assert.Null(result.Rows[0].Error);
    }
    [Fact]
    public void Parse_SemicolonDelimiterAndDecimalComma_IsAccepted()
    {
        string header = Header.Replace(',', ';');
        string row = "1;120,50;0,15;800;20;450;SP;RJ;cama_mesa_banho;credit_card;1;2";
        BatchCsvParseResult result = Parse($"{header}\r\n{row}\r\n");
        Assert.Empty(result.FileErrors);
        Assert.Equal(120.5f, result.Rows[0].Features!.TotalPrice);
        Assert.Equal(0.15f, result.Rows[0].Features!.FreightShare);
    }
    [Fact]
    public void Parse_EmptyOptionalValues_BecomeNaN()
    {
        string row = "1,120.50,0.15,,,NaN,SP,RJ,cama_mesa_banho,credit_card,,2";
        BatchCsvParseResult result = Parse($"{Header}\r\n{row}\r\n");
        Assert.Null(result.Rows[0].Error);
        Assert.True(float.IsNaN(result.Rows[0].Features!.TotalWeightG));
        Assert.True(float.IsNaN(result.Rows[0].Features!.PromisedDays));
        Assert.True(float.IsNaN(result.Rows[0].Features!.DistanceKm));
        Assert.True(float.IsNaN(result.Rows[0].Features!.Installments));
    }
    [Fact]
    public void Parse_MissingColumns_ReturnsFileErrorWithNames()
    {
        BatchCsvParseResult result = Parse("itemsCount,totalPrice\r\n1,100\r\n");
        Assert.Single(result.FileErrors);
        Assert.Contains("Missing columns", result.FileErrors[0]);
        Assert.Contains("freightShare", result.FileErrors[0]);
        Assert.Empty(result.Rows);
    }
    [Theory]
    [InlineData("")]
    [InlineData("   \r\n")]
    public void Parse_EmptyFile_ReturnsFileError(string text)
    {
        BatchCsvParseResult result = Parse(text);
        Assert.Single(result.FileErrors);
    }
    [Fact]
    public void Parse_HeaderOnly_ReturnsFileError()
    {
        BatchCsvParseResult result = Parse(Header + "\r\n");
        Assert.Contains("no data rows", result.FileErrors[0]);
    }
    [Fact]
    public void Parse_BadRow_IsReportedWithoutStoppingOtherRows()
    {
        string badNumber = "abc,120.50,0.15,800,20,450,SP,RJ,cama_mesa_banho,credit_card,1,2";
        string outOfRange = "1,120.50,2,800,20,450,SP,RJ,cama_mesa_banho,credit_card,1,2";
        BatchCsvParseResult result = Parse($"{Header}\r\n{badNumber}\r\n{GoodRow}\r\n{outOfRange}\r\n");
        Assert.Empty(result.FileErrors);
        Assert.Equal(3, result.Rows.Count);
        Assert.NotNull(result.Rows[0].Error);
        Assert.Contains("itemsCount", result.Rows[0].Error);
        Assert.Null(result.Rows[1].Error);
        Assert.NotNull(result.Rows[1].Features);
        Assert.Contains("freightShare", result.Rows[2].Error);
        Assert.Null(result.Rows[2].Features);
    }
    [Fact]
    public void Parse_TooManyRows_ReturnsFileError()
    {
        StringBuilder text = new StringBuilder();
        text.Append(Header).Append("\r\n");
        for (int i = 0; i < BatchCsv.MaxRows + 1; i++)
        {
            text.Append(GoodRow).Append("\r\n");
        }
        BatchCsvParseResult result = Parse(text.ToString());
        Assert.Contains("Too many rows", result.FileErrors[0]);
    }
        [Fact]
    public void Parse_TooManyRows_StopsReadingTheStream()
    {
        StringBuilder text = new StringBuilder(Header + "\r\n");
        for (int i = 0; i < BatchCsv.MaxRows + 10000; i++)
        {
            text.Append(GoodRow).Append("\r\n");
        }
        using CountingStream stream = new CountingStream(Encoding.UTF8.GetBytes(text.ToString()));

        BatchCsvParseResult result = BatchCsv.Parse(stream);

        Assert.Contains("Too many rows", result.FileErrors[0]);
        Assert.True(stream.BytesRead < stream.Length - 100000, $"Read {stream.BytesRead} of {stream.Length} bytes");
    }
    private sealed class CountingStream(byte[] data) : MemoryStream(data)
    {
        public long BytesRead
        {
            get;
            private set;
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = base.Read(buffer, offset, count);
            BytesRead += read;
            return read;
        }
        public override int Read(Span<byte> buffer)
        {
            int read = base.Read(buffer);
            BytesRead += read;
            return read;
        }
    }
}
[Collection(ApiCollection.Name)]
public class BatchTemplateTests(ApiFactory factory)
{
    private async Task<HttpClient> ClientAsync(string role)
    {
        string email = await factory.CreateUserAsync(role);
        AuthResponse auth = await factory.LoginAsync(email);
        return factory.CreateClient(auth.AccessToken);
    }
    [Fact]
    public async Task Template_AsAnalyst_ReturnsCsvThatParsesWithoutErrors()
    {
        HttpClient client = await ClientAsync(Roles.Analyst);
        HttpResponseMessage response = await client.GetAsync("/api/batch-jobs/template");
        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
        using MemoryStream stream = new MemoryStream(bytes);
        BatchCsvParseResult parsed = BatchCsv.Parse(stream);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Empty(parsed.FileErrors);
        Assert.Equal(2, parsed.Rows.Count);
        Assert.All(parsed.Rows, row => Assert.Null(row.Error));
    }
    [Fact]
    public async Task Template_AsManager_Returns403()
    {
        HttpClient client = await ClientAsync(Roles.Manager);
        HttpResponseMessage response = await client.GetAsync("/api/batch-jobs/template");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}