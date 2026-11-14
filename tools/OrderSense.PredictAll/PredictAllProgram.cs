using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using OrderSense.Api.Data;
using OrderSense.Api.Data.Entities;
using OrderSense.Api.Predictions;
using System.Net.Http.Json;
using System.Text.Json;
using OrderSense.Contracts;
namespace OrderSense.PredictAll;
public static class PredictAllProgram
{
    public static async Task<int> Main(string[] args)
    {
        string? connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__ordersense");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.Error.WriteLine("Не задано змінну середовища ConnectionStrings__ordersense.");
            return 1;
        }
        bool recalculate = args.Contains("--recalculate");
        string[] positional = args.Where(a => !a.StartsWith("--")).ToArray();
        string serviceUrl = positional.Length > 0 ? positional[0] : "http://localhost:5002";
        int maxOrders = positional.Length > 1 ? int.Parse(positional[1]) : int.MaxValue;
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        await using AppDbContext db = new AppDbContext(options);
        using HttpClient http = new HttpClient();
        http.BaseAddress = new Uri(serviceUrl);
        http.Timeout = TimeSpan.FromMinutes(2);
        string? targetVersion = null;

        if (recalculate)
        {
            ModelDetails? details = await http.GetFromJsonAsync<ModelDetails>("/model", new JsonSerializerOptions(JsonSerializerDefaults.Web));

            if (details is null || string.IsNullOrWhiteSpace(details.Version))
            {
                Console.Error.WriteLine("Не вдалося отримати версію моделі з /model.");

                return 1;
            }

            targetVersion = details.Version;
            Console.WriteLine($"Перерахунок: усім замовленням без прогнозу версії {targetVersion}.");
        }
        BatchPredictor predictor = new BatchPredictor(db, http) { RecalculateToVersion = targetVersion };
        Stopwatch watch = Stopwatch.StartNew();
        BatchRunResult result = await predictor.RunAsync(1000, maxOrders, CancellationToken.None, (progress, _) =>
        {
            Console.WriteLine($"Партія {progress.Batches}: разом записано {progress.Predicted}, пропущено {progress.Skipped}, {watch.Elapsed:mm\\:ss}");

            return Task.CompletedTask;
        });
        Console.WriteLine();
        Console.WriteLine($"Готово: записано {result.Predicted}, пропущено {result.Skipped}, партій {result.Batches}.");
        int low = await db.Predictions.CountAsync(p => p.RiskLevel == RiskLevel.Low);
        int medium = await db.Predictions.CountAsync(p => p.RiskLevel == RiskLevel.Medium);
        int high = await db.Predictions.CountAsync(p => p.RiskLevel == RiskLevel.High);
        Console.WriteLine($"Прогнозів у базі: low {low}, medium {medium}, high {high}.");
        foreach (var group in await db.Predictions.GroupBy(p => p.ModelVersion.Version).Select(g => new { g.Key, Count = g.Count() }).ToListAsync())
        {
            Console.WriteLine($"Версія {group.Key}: {group.Count} прогнозів.");
        }
        return 0;
    }
}