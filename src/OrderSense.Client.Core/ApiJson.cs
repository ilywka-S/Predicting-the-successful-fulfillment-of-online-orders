using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrderSense.Client.Core;

public partial class Client
{
    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings)
    {
        settings.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }
}

[JsonConverter(typeof(RiskLevel2JsonConverter))]
public partial class RiskLevel2
{
    public RiskLevel Value { get; init; }

    public override string ToString() => Value.ToString();
}

public sealed class RiskLevel2JsonConverter : JsonConverter<RiskLevel2>
{
    public override RiskLevel2? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            reader.Skip();
            return null;
        }

        return reader.GetString() switch
        {
            "low" => new RiskLevel2 { Value = RiskLevel.Low },
            "medium" => new RiskLevel2 { Value = RiskLevel.Medium },
            "high" => new RiskLevel2 { Value = RiskLevel.High },
            _ => null,
        };
    }

    public override void Write(Utf8JsonWriter writer, RiskLevel2 value, JsonSerializerOptions options) =>
        writer.WriteStringValue(JsonNamingPolicy.CamelCase.ConvertName(value.Value.ToString()));
}
