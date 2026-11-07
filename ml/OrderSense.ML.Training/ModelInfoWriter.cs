using System.Text.Json;
using Microsoft.ML;
using Microsoft.ML.Data;
using OrderSense.Contracts;

namespace OrderSense.ML.Training;

public static class ModelInfoWriter
{
    private static readonly string[] FeatureNames =
    [
        "ItemsCount", "TotalPrice", "FreightShare", "TotalWeightG", "PromisedDays", "DistanceKm",
        "Installments", "PurchaseDayOfWeek", "CustomerState", "SellerState", "MainCategory", "PaymentType",
    ];

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static void Write(
        MLContext mlContext,
        ITransformer model,
        List<TrainingRow> trainRows,
        List<TrainingRow> testRows,
        BinaryClassificationMetrics metrics,
        BinaryClassificationMetrics baselineMetrics,
        string modelDirectory,
        string version)
    {
        float basePrAuc = (float)metrics.AreaUnderPrecisionRecallCurve;

        ModelDetails info = new ModelDetails
        {
            Version = version,
            TrainedAt = DateTimeOffset.UtcNow,
            TrainRows = trainRows.Count,
            TestRows = testRows.Count,
            TrainPeriod = Period(trainRows),
            TestPeriod = Period(testRows),
            RocAuc = (float)metrics.AreaUnderRocCurve,
            PrAuc = basePrAuc,
            F1 = (float)metrics.F1Score,
            BaselinePrAuc = (float)baselineMetrics.AreaUnderPrecisionRecallCurve,
            FeatureImportances = ComputeImportances(mlContext, model, testRows, basePrAuc),
        };

        string json = JsonSerializer.Serialize(info, JsonOptions);
        string path = Path.Combine(modelDirectory, "model-info.json");
        File.WriteAllText(path, json);

        Console.WriteLine($"Збережено: {path}");
    }

    private static string Period(List<TrainingRow> rows)
    {
        DateTime first = DateTime.MaxValue;
        DateTime last = DateTime.MinValue;

        foreach (TrainingRow row in rows)
        {
            if (row.PurchasedAt < first)
            {
                first = row.PurchasedAt;
            }

            if (row.PurchasedAt > last)
            {
                last = row.PurchasedAt;
            }
        }

        return $"{first:yyyy-MM} – {last:yyyy-MM}";
    }

    private static List<FeatureImportance> ComputeImportances(
        MLContext mlContext, ITransformer model, List<TrainingRow> testRows, float basePrAuc)
    {
        const int repeats = 3;
        Random random = new Random(42);
        List<FeatureImportance> result = new List<FeatureImportance>();

        foreach (string feature in FeatureNames)
        {
            double totalDrop = 0;

            for (int repeat = 0; repeat < repeats; repeat++)
            {
                List<TrainingRow> permuted = Permute(testRows, feature, random);
                IDataView data = mlContext.Data.LoadFromEnumerable(permuted);
                IDataView scored = model.Transform(data);
                BinaryClassificationMetrics permutedMetrics =
                    mlContext.BinaryClassification.Evaluate(scored, labelColumnName: "IsProblem");

                totalDrop += basePrAuc - permutedMetrics.AreaUnderPrecisionRecallCurve;
            }

            result.Add(new FeatureImportance
            {
                Feature = feature,
                Importance = (float)(totalDrop / repeats),
            });
        }

        result.Sort(CompareByImportanceDescending);

        return result;
    }

    private static int CompareByImportanceDescending(FeatureImportance a, FeatureImportance b)
    {
        return b.Importance.CompareTo(a.Importance);
    }

    private static List<TrainingRow> Permute(List<TrainingRow> rows, string feature, Random random)
    {
        int count = rows.Count;
        int[] order = new int[count];

        for (int i = 0; i < count; i++)
        {
            order[i] = i;
        }

        for (int i = count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            int temp = order[i];
            order[i] = order[j];
            order[j] = temp;
        }

        List<TrainingRow> copy = new List<TrainingRow>(count);

        for (int i = 0; i < count; i++)
        {
            copy.Add(Clone(rows[i]));
        }

        for (int i = 0; i < count; i++)
        {
            Assign(copy[i], rows[order[i]], feature);
        }

        return copy;
    }

    private static TrainingRow Clone(TrainingRow row)
    {
        return new TrainingRow
        {
            ItemsCount = row.ItemsCount,
            TotalPrice = row.TotalPrice,
            FreightShare = row.FreightShare,
            TotalWeightG = row.TotalWeightG,
            PromisedDays = row.PromisedDays,
            DistanceKm = row.DistanceKm,
            CustomerState = row.CustomerState,
            SellerState = row.SellerState,
            MainCategory = row.MainCategory,
            PaymentType = row.PaymentType,
            Installments = row.Installments,
            PurchaseDayOfWeek = row.PurchaseDayOfWeek,
            Label = row.Label,
            IsProblem = row.IsProblem,
            PurchasedAt = row.PurchasedAt,
        };
    }

    private static void Assign(TrainingRow target, TrainingRow source, string feature)
    {
        switch (feature)
        {
            case "ItemsCount":
                target.ItemsCount = source.ItemsCount;
                break;
            case "TotalPrice":
                target.TotalPrice = source.TotalPrice;
                break;
            case "FreightShare":
                target.FreightShare = source.FreightShare;
                break;
            case "TotalWeightG":
                target.TotalWeightG = source.TotalWeightG;
                break;
            case "PromisedDays":
                target.PromisedDays = source.PromisedDays;
                break;
            case "DistanceKm":
                target.DistanceKm = source.DistanceKm;
                break;
            case "Installments":
                target.Installments = source.Installments;
                break;
            case "PurchaseDayOfWeek":
                target.PurchaseDayOfWeek = source.PurchaseDayOfWeek;
                break;
            case "CustomerState":
                target.CustomerState = source.CustomerState;
                break;
            case "SellerState":
                target.SellerState = source.SellerState;
                break;
            case "MainCategory":
                target.MainCategory = source.MainCategory;
                break;
            case "PaymentType":
                target.PaymentType = source.PaymentType;
                break;
        }
    }
}