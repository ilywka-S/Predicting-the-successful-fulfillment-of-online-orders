using System.Text.Json.Serialization;
using Microsoft.Extensions.ML;
using Microsoft.Extensions.Options;
using OrderSense.Contracts;
using OrderSense.PredictionService;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();

// NaN у JSON передаємо як "NaN"
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals);

builder.Services.Configure<RiskThresholds>(builder.Configuration.GetSection("Risk"));

var modelVersion = builder.Configuration["Model:Version"] ?? "v1";
var modelPath = Path.Combine(AppContext.BaseDirectory, builder.Configuration["Model:Path"] ?? "models/v1/model.zip");

builder.Services.AddSingleton(new ModelInfo(modelVersion));
builder.Services.AddSingleton<ExplainService>();
builder.Services.AddPredictionEnginePool<OrderFeatures, ModelOutput>()
    .FromFile(modelName: modelVersion, filePath: modelPath, watchForChanges: false);

var app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapPost("/predict", (OrderFeatures features,
    PredictionEnginePool<OrderFeatures, ModelOutput> pool,
    IOptions<RiskThresholds> risk,
    ModelInfo model) =>
{
    var errors = PredictValidator.Validate(features);
    if (errors.Count > 0) return Results.ValidationProblem(errors);

    var pProblem = pool.Predict(model.Version, features).Probability;

    return Results.Ok(new OrderPrediction
    {
        Probability = 1f - pProblem,          // ймовірність УСПІХУ
        RiskLevel = risk.Value.Level(pProblem),
        ModelVersion = model.Version
    });
});

app.MapPost("/explain", (OrderFeatures features, ExplainService explain, ModelInfo model) =>
{
    var errors = PredictValidator.Validate(features);
    if (errors.Count > 0) return Results.ValidationProblem(errors);

    return Results.Ok(new OrderExplanation
    {
        ModelVersion = model.Version,
        Factors = explain.Explain(features)
    });
});

app.Run();

public partial class Program { }