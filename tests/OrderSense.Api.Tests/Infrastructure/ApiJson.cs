using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrderSense.Api.Tests.Infrastructure;

public static class ApiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}