using System.Text.Json;
using Microsoft.Extensions.Options;
using OrderSense.Contracts;
namespace OrderSense.PredictionService;
public sealed class ModelInfoService
{
    public ModelInfoService(IConfiguration configuration, IOptions<RiskThresholds> risk)
    {
        string modelPath = Path.Combine(AppContext.BaseDirectory, configuration["Model:Path"] ?? "models/v1/model.zip");
        string folder = Path.GetDirectoryName(modelPath) ?? AppContext.BaseDirectory;
        string infoPath = Path.Combine(folder, "model-info.json");
        if (!File.Exists(infoPath))
        {
            return;
        }
        string json = File.ReadAllText(infoPath);
        JsonSerializerOptions options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        ModelDetails? details = JsonSerializer.Deserialize<ModelDetails>(json, options);
        if (details == null)
        {
            return;
        }
        details.RiskMediumFrom = risk.Value.MediumFrom;
        details.RiskHighFrom = risk.Value.HighFrom;
        Details = details;
    }
    public ModelDetails? Details { get; }
}