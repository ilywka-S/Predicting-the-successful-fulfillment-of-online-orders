using System.Net;

namespace OrderSense.Client.Core.Tests;

public class ClientBaseUrlTests
{
    [Fact]
    public async Task Requests_GoToHttpClientBaseAddress()
    {
        var handler = new RecordingHandler();
        var client = new Client(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test") });
        await Assert.ThrowsAnyAsync<Exception>(() => client.LoginAsync(new LoginRequest { Email = "a@b.c", Password = "x" }));

        Assert.Equal(new Uri("https://api.example.test/api/auth/login"), handler.LastRequestUri);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }
    }
}