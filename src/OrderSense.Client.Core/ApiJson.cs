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

public interface INullableRiskLevel
{
    RiskLevel Value { get; set; }
}

[JsonConverter(typeof(NullableRiskLevelJsonConverter<RiskLevel2>))]
public partial class RiskLevel2 : INullableRiskLevel
{
    public RiskLevel Value { get; set; }

    public override string ToString() => Value.ToString();
}

[JsonConverter(typeof(NullableRiskLevelJsonConverter<RiskLevel3>))]
public partial class RiskLevel3 : INullableRiskLevel
{
    public RiskLevel Value { get; set; }

    public override string ToString() => Value.ToString();
}

public sealed class NullableRiskLevelJsonConverter<T> : JsonConverter<T>
    where T : class, INullableRiskLevel, new()
{
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            reader.Skip();
            return null;
        }

        RiskLevel? level = reader.GetString() switch
        {
            "low" => RiskLevel.Low,
            "medium" => RiskLevel.Medium,
            "high" => RiskLevel.High,
            _ => null,
        };

        return level is { } value ? new T { Value = value } : null;
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        writer.WriteStringValue(JsonNamingPolicy.CamelCase.ConvertName(value.Value.ToString()));
}
