using Microsoft.EntityFrameworkCore;
using OrderSense.Api.Data;
using OrderSense.Api.Data.Entities;
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
        string serviceUrl = "http://localhost:5002";
        if (args.Length > 0)
        {
            serviceUrl = args[0];
        }
        int maxOrders = int.MaxValue;
        if (args.Length > 1)
        {
            maxOrders = int.Parse(args[1]);
        }
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        await using AppDbContext db = new AppDbContext(options);
        using HttpClient http = new HttpClient();
        http.BaseAddress = new Uri(serviceUrl);
        http.Timeout = TimeSpan.FromMinutes(2);
        BatchPredictor predictor = new BatchPredictor(db, http);
        BatchRunResult result = await predictor.RunAsync(1000, maxOrders, CancellationToken.None);
        Console.WriteLine();
        Console.WriteLine($"Готово: записано {result.Predicted}, пропущено {result.Skipped}, партій {result.Batches}.");
        int low = await db.Predictions.CountAsync(p => p.RiskLevel == RiskLevel.Low);
        int medium = await db.Predictions.CountAsync(p => p.RiskLevel == RiskLevel.Medium);
        int high = await db.Predictions.CountAsync(p => p.RiskLevel == RiskLevel.High);
        Console.WriteLine($"Прогнозів у базі: low {low}, medium {medium}, high {high}.");
        return 0;
    }
}