using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace OrderSense.Api.Tests.Infrastructure;

public static class ProblemAssert
{
    public static async Task<ProblemDetails> StatusAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        return (await response.Content.ReadFromJsonAsync<ProblemDetails>())!;
    }

    public static async Task ValidationAsync(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Contains(field, problem!.Errors.Keys, StringComparer.OrdinalIgnoreCase);
    }
}