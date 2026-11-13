using System.Net;
using System.Net.Http.Json;
using System.Text;
using OrderSense.Api.Auth;
using OrderSense.Api.Dtos;
using OrderSense.Api.Predictions;
using OrderSense.Api.Tests.Infrastructure;

namespace OrderSense.Api.Tests.BatchJobs;

[Collection(ApiCollection.Name)]
public class BatchJobsEdgeCaseTests(ApiFactory factory)
{
    private const string ValidRow = "1,120.50,0.15,800,20,450,SP,RJ,cama_mesa_banho,credit_card,1,2";

    [Theory]
    [InlineData("/api/batch-jobs/not-a-guid")]
    [InlineData("/api/batch-jobs/00000000-0000-0000-0000-000000000000")]
    [InlineData("/api/batch-jobs/00000000-0000-0000-0000-000000000000/results")]
    [InlineData("/api/batch-jobs/00000000-0000-0000-0000-000000000000/export")]
    public async Task UnknownJob_Returns404(string url)
    {
        var client = await AnalystClientAsync();

        var response = await client.GetAsync(url);

        await ProblemAssert.StatusAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_JsonInsteadOfFile_Returns415()
    {
        var client = await AnalystClientAsync();

        var response = await client.PostAsJsonAsync("/api/batch-jobs", new { });

        await ProblemAssert.StatusAsync(response, HttpStatusCode.UnsupportedMediaType);
    }

    [Theory]
    [InlineData(new byte[] { 0, 1, 2, 255, 254, 0, 13, 10 })]
    [InlineData(new byte[] { 0xEF, 0xBB, 0xBF })]
    public async Task Create_BinaryOrEmptyFile_Returns400(byte[] bytes)
    {
        var client = await AnalystClientAsync();

        var response = await client.PostAsync("/api/batch-jobs", Upload(bytes, "data.csv"));

        await ProblemAssert.ValidationAsync(response, "file");
    }

    [Theory]
    [InlineData("")]
    [InlineData("\"1,120.50,0.15\r\n")]
    public async Task Create_HeaderWithoutValidRows_Returns400(string body)
    {
        var client = await AnalystClientAsync();

        var response = await client.PostAsync("/api/batch-jobs", Upload(Encoding.UTF8.GetBytes(BatchCsv.Header + "\r\n" + body), "data.csv"));

        await ProblemAssert.ValidationAsync(response, "file");
    }

    [Fact]
    public async Task Create_VeryLongFileName_IsShortenedTo255()
    {
        var client = await AnalystClientAsync();
        var csv = Encoding.UTF8.GetBytes(BatchCsv.Header + "\r\n" + ValidRow + "\r\n");

        var response = await client.PostAsync("/api/batch-jobs", Upload(csv, new string('n', 400) + ".csv"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var job = await response.Content.ReadFromJsonAsync<BatchJobDto>(ApiJson.Options);
        Assert.Equal(255, job!.FileName.Length);
    }

    private async Task<HttpClient> AnalystClientAsync() => factory.CreateClient((await factory.LoginAsync(await factory.CreateUserAsync(Roles.Analyst))).AccessToken);

    private static MultipartFormDataContent Upload(byte[] bytes, string fileName) => new() { { new ByteArrayContent(bytes), "file", fileName } };
}