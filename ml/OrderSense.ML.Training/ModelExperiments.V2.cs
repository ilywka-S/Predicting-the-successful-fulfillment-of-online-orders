using System.Globalization;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace OrderSense.ML.Training;

public static partial class ModelExperiments
{
    private const string V2Version = "v2";

    public static void TrainV2(MLContext ml, List<TrainingRow> trainRows, List<TrainingRow> testRows)
    {
        DateTime calibStart = new DateTime(2018, 4, 1);
        List<TrainingRow> fitRows = trainRows.Where(r => r.PurchasedAt < calibStart).ToList();
        List<TrainingRow> calibRows = trainRows.Where(r => r.PurchasedAt >= calibStart).ToList();

        FeatureSet features = new FeatureSet("10 ознак",
            Numeric.Where(n => n != "PurchaseDayOfWeek").ToArray(),
            Categorical.Where(n => n != "MainCategory").ToArray());
        Candidate candidate = new Candidate("v2 FastTree l=8 lr=0.05 t=300, 10 ознак", features, FastTree(8, 0.05, 300));

        Console.WriteLine($"\n=== Навчання {V2Version}: fit {fitRows.Count}, calib {calibRows.Count}, test {testRows.Count} ===");

        IDataView fitData = ml.Data.LoadFromEnumerable(fitRows);
        ITransformer model = Build(ml, candidate).Fit(fitData);

        IDataView calibScored = model.Transform(ml.Data.LoadFromEnumerable(calibRows));
        ITransformer dropProbability = ml.Transforms.DropColumns("Probability").Fit(calibScored);
        ITransformer platt = ml.BinaryClassification.Calibrators.Platt(labelColumnName: "IsProblem")
            .Fit(dropProbability.Transform(calibScored));

        ITransformer calibratedModel = new TransformerChain<ITransformer>(new ITransformer[] { model, dropProbability, platt });

        IDataView testScored = calibratedModel.Transform(ml.Data.LoadFromEnumerable(testRows));
        var metrics = ml.BinaryClassification.Evaluate(testScored, labelColumnName: "IsProblem");

        float baseRate = fitRows.Count(r => r.IsProblem) / (float)fitRows.Count;
        IDataView baselineData = ml.Data.LoadFromEnumerable(testRows.Select(r => new BaselinePrediction
        {
            IsProblem = r.IsProblem,
            Score = baseRate,
            Probability = baseRate,
            PredictedLabel = false,
        }));
        var baseMetrics = ml.BinaryClassification.Evaluate(baselineData, labelColumnName: "IsProblem");

        Console.WriteLine("| Модель | ROC-AUC | PR-AUC | LogLoss | Brier | ECE | Середня p |");
        Result? row = Evaluate(ml, "v2 (FastTree, 10 ознак, Platt)", calibratedModel, testRows);

        if (row is not null)
        {
            PrintRow(row);
        }

        Console.WriteLine($"Базова модель: ROC-AUC {baseMetrics.AreaUnderRocCurve:F4}, PR-AUC {baseMetrics.AreaUnderPrecisionRecallCurve:F4}");
        Console.WriteLine($"Частка проблем: fit {Rate(fitRows):P2}, calib {Rate(calibRows):P2}, test {Rate(testRows):P2}");

        string modelDir = $"../../ml/models/{V2Version}";
        Directory.CreateDirectory(modelDir);
        string modelPath = Path.Combine(modelDir, "model.zip");
        ml.Model.Save(calibratedModel, fitData.Schema, modelPath);
        Console.WriteLine($"Модель збережено: {modelPath}");

        ITransformer loaded = ml.Model.Load(modelPath, out _);
        List<TrainingRow> sample = testRows.Take(2000).ToList();
        float[] expected = Probabilities(ml, calibratedModel, sample);
        float[] actual = Probabilities(ml, loaded, sample);
        float maxDiff = expected.Select((v, i) => Math.Abs(v - actual[i])).Max();
        Console.WriteLine($"Перевірка збереження: максимальна різниця ймовірностей {maxDiff.ToString("E2", CultureInfo.InvariantCulture)} (має бути ~0)");

        ModelInfoWriter.Write(ml, calibratedModel, fitRows, testRows, metrics, baseMetrics, modelDir, V2Version);

        double calibRate = Rate(calibRows);
        float mediumFrom = (float)Math.Round(1.5 * calibRate, 2, MidpointRounding.AwayFromZero);
        float highFrom = (float)Math.Round(2.5 * calibRate, 2, MidpointRounding.AwayFromZero);

        Console.WriteLine($"\nПропоновані пороги: medium від {mediumFrom.ToString("F2", CultureInfo.InvariantCulture)}, high від {highFrom.ToString("F2", CultureInfo.InvariantCulture)} (частота у calib {calibRate.ToString("P2", CultureInfo.InvariantCulture)})");

        List<ScoredRow> scored = ml.Data.CreateEnumerable<ScoredRow>(testScored, reuseRowObject: false).ToList();
        Console.WriteLine("Рівні ризику на тесті: рівень | частка замовлень | фактична частка проблем");

        foreach (string level in new[] { "low", "medium", "high" })
        {
            List<ScoredRow> group = scored.Where(x => LevelOf(x.Probability, mediumFrom, highFrom) == level).ToList();

            if (group.Count == 0)
            {
                Console.WriteLine($"  {level,-6} | 0 | -");

                continue;
            }

            Console.WriteLine($"  {level,-6} | {(group.Count / (double)scored.Count).ToString("P1", CultureInfo.InvariantCulture)} | {group.Average(x => x.IsProblem ? 1.0 : 0.0).ToString("P1", CultureInfo.InvariantCulture)}");
        }
    }

    private static string LevelOf(float pProblem, float mediumFrom, float highFrom)
    {
        return pProblem >= highFrom ? "high" : pProblem >= mediumFrom ? "medium" : "low";
    }

    private static float[] Probabilities(MLContext ml, ITransformer model, List<TrainingRow> rows)
    {
        return ml.Data
            .CreateEnumerable<ScoredRow>(model.Transform(ml.Data.LoadFromEnumerable(rows)), reuseRowObject: false)
            .Select(x => x.Probability)
            .ToArray();
    }
}