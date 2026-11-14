using System.Globalization;
using Microsoft.ML;
using Microsoft.ML.AutoML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms;
using OrderSense.Contracts;
namespace OrderSense.ML.Training;
public static partial class ModelExperiments
{
    private const int AutoMlSeconds = 600;
    private static readonly string[] Numeric =
    [
        "ItemsCount", "TotalPrice", "FreightShare", "TotalWeightG", "PromisedDays", "DistanceKm", "Installments", "PurchaseDayOfWeek",
    ];
    private static readonly string[] Categorical = ["CustomerState", "SellerState", "MainCategory", "PaymentType"];
    private delegate IEstimator<ITransformer> TrainerFactory(MLContext ml, string featureColumn);
    private sealed record FeatureSet(string Name, string[] Numeric, string[] Categorical);
    private sealed record Candidate(string Name, FeatureSet Features, TrainerFactory Trainer, int? MiSlots = null);
    private sealed record Result(string Name, double RocAuc, double PrAuc, double LogLoss, double Brier, double Ece, double MeanProbability);
    private sealed class ScoredRow
    {
        public bool IsProblem 
        { 
            get; 
            set; 
        }
        public float Probability 
        { 
            get; 
            set; 
        }
    }
    public static void Run(MLContext ml, List<TrainingRow> trainRows, List<TrainingRow> testRows)
    {
        DateTime validStart = new DateTime(2018, 4, 1);
        List<TrainingRow> fitRows = trainRows.Where(r => r.PurchasedAt < validStart).ToList();
        List<TrainingRow> validRows = trainRows.Where(r => r.PurchasedAt >= validStart).ToList();
        Console.WriteLine($"\n=== Експерименти: fit {fitRows.Count}, valid {validRows.Count}, train(повний) {trainRows.Count}, test {testRows.Count} ===");
        Console.WriteLine($"Частка проблем: fit {Rate(fitRows):P1}, valid {Rate(validRows):P1}, test {Rate(testRows):P1}");
        FeatureSet allFeatures = new FeatureSet("усі 12", Numeric, Categorical);
        Candidate probe = new Candidate("probe", allFeatures, FastTree(20, 0.2, 100));
        ITransformer probeModel = Build(ml, probe).Fit(ml.Data.LoadFromEnumerable(fitRows));
        var probeMetrics = ml.BinaryClassification.Evaluate(probeModel.Transform(ml.Data.LoadFromEnumerable(validRows)), "IsProblem");
        List<FeatureImportance> importances = ModelInfoWriter.ComputeImportances(ml, probeModel, validRows, (float)probeMetrics.AreaUnderPrecisionRecallCurve);
        Console.WriteLine("\n--- PFI на валідації (падіння PR-AUC при перемішуванні) ---");
        foreach (FeatureImportance item in importances)
        {
            Console.WriteLine($"  {item.Feature,-18} {item.Importance.ToString("F5", CultureInfo.InvariantCulture)}");
        }
        string[] kept = importances.Where(i => i.Importance > 0).Select(i => i.Feature).ToArray();
        FeatureSet pfiSet = new FeatureSet($"PFI>0 ({kept.Length})",
            Numeric.Where(n => kept.Contains(n)).ToArray(),
            Categorical.Where(n => kept.Contains(n)).ToArray());

        List<Candidate> grid = [];
        foreach (int leaves in new[] { 8, 20, 50 })
        {
            foreach (double lr in new[] { 0.05, 0.2 })
            {
                foreach (int trees in new[] { 100, 300 })
                {
                    grid.Add(new Candidate($"FastTree l={leaves} lr={lr} t={trees}", allFeatures, FastTree(leaves, lr, trees)));
                }
            }
        }
        foreach (int leaves in new[] { 8, 20, 50 })
        {
            foreach (double lr in new[] { 0.03, 0.1 })
            {
                foreach (int iterations in new[] { 200, 500 })
                {
                    grid.Add(new Candidate($"LightGBM l={leaves} lr={lr} it={iterations}", allFeatures, LightGbm(leaves, lr, iterations)));
                }
            }
        }
        Console.WriteLine("\n--- Валідація (2018-04 – 2018-05), навчання на fit ---");
        List<(Candidate Candidate, Result Result)> gridResults = [];
        foreach (Candidate candidate in grid)
        {
            Result? result = FitAndEvaluate(ml, candidate, fitRows, validRows);
            if (result is not null)
            {
                gridResults.Add((candidate, result));
                PrintRow(result);
            }
        }
        Candidate bestFastTree = gridResults.Where(x => x.Candidate.Name.StartsWith("FastTree")).MaxBy(x => x.Result.PrAuc).Candidate;
        Candidate bestLightGbm = gridResults.Where(x => x.Candidate.Name.StartsWith("LightGBM")).MaxBy(x => x.Result.PrAuc).Candidate;
        Candidate bestOnAll = gridResults.MaxBy(x => x.Result.PrAuc).Candidate;
        Console.WriteLine($"\nКраща FastTree: {bestFastTree.Name}\nКраща LightGBM: {bestLightGbm.Name}\nКраща загалом: {bestOnAll.Name}");

        List<Candidate> selection =
        [
            bestOnAll with { Name = $"{bestOnAll.Name} + {pfiSet.Name}", Features = pfiSet },
            bestOnAll with { Name = $"{bestOnAll.Name} + MI 40 слотів", MiSlots = 40 },
            bestOnAll with { Name = $"{bestOnAll.Name} + MI 20 слотів", MiSlots = 20 },
        ];
        Console.WriteLine("\n--- Відбір ознак (валідація) ---");
        PrintRow(gridResults.First(x => x.Candidate == bestOnAll).Result);
        List<(Candidate Candidate, Result Result)> selectionResults = [];
        foreach (Candidate candidate in selection)
        {
            Result? result = FitAndEvaluate(ml, candidate, fitRows, validRows);
            if (result is not null)
            {
                selectionResults.Add((candidate, result));
                PrintRow(result);
            }
        }
        Candidate best = bestOnAll;
        double bestPr = gridResults.First(x => x.Candidate == bestOnAll).Result.PrAuc;
        foreach ((Candidate candidate, Result result) in selectionResults)
        {
            if (result.PrAuc > bestPr)
            {
                best = candidate;
                bestPr = result.PrAuc;
            }
        }
        Console.WriteLine($"\nОбрана конфігурація: {best.Name}");

        Console.WriteLine("\n=== ТЕСТ (2018-06 – 2018-08), навчання на повному train ===");
        Console.WriteLine("| Модель | ROC-AUC | PR-AUC | LogLoss | Brier | ECE | Середня p | (частка проблем у тесті: " + Rate(testRows).ToString("P2", CultureInfo.InvariantCulture) + ") |");
        List<Candidate> finalists =
        [
            new Candidate("v1 FastTree (за замовчуванням)", allFeatures, FastTree(20, 0.2, 100)),
            bestFastTree with { Name = $"FastTree підібрана ({bestFastTree.Name})" },
            bestLightGbm with { Name = $"LightGBM підібрана ({bestLightGbm.Name})" },
        ];
        if (best != bestOnAll)
        {
            finalists.Add(best with { Name = $"Обрана + відбір ознак ({best.Name})" });
        }
        foreach (Candidate candidate in finalists)
        {
            Result? result = FitAndEvaluate(ml, candidate, trainRows, testRows);
            if (result is not null)
            {
                PrintRow(result);
            }
        }
        RunAutoMl(ml, fitRows, validRows, testRows);
        RunCalibration(ml, best, fitRows, validRows, testRows);
    }
    private static void RunAutoMl(MLContext ml, List<TrainingRow> fitRows, List<TrainingRow> validRows, List<TrainingRow> testRows)
    {
        Console.WriteLine($"\n--- AutoML ({AutoMlSeconds} с, оптимізація PR-AUC; навчання на fit, валідація на valid) ---");
        try
        {
            ColumnInformation columns = new ColumnInformation { LabelColumnName = "IsProblem" };
            foreach (string name in Numeric)
            {
                columns.NumericColumnNames.Add(name);
            }
            foreach (string name in Categorical)
            {
                columns.CategoricalColumnNames.Add(name);
            }
            columns.IgnoredColumnNames.Add("Label");
            columns.IgnoredColumnNames.Add("PurchasedAt");
            BinaryExperimentSettings settings = new BinaryExperimentSettings
            {
                MaxExperimentTimeInSeconds = AutoMlSeconds,
                OptimizingMetric = BinaryClassificationMetric.AreaUnderPrecisionRecallCurve,
            };
            ExperimentResult<BinaryClassificationMetrics> experiment = ml.Auto()
                .CreateBinaryClassificationExperiment(settings)
                .Execute(ml.Data.LoadFromEnumerable(fitRows), ml.Data.LoadFromEnumerable(validRows), columns);
            string trainer = experiment.BestRun.TrainerName;
            Result? result = Evaluate(ml, $"AutoML ({trainer})", experiment.BestRun.Model, testRows);
            if (result is not null)
            {
                PrintRow(result);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"AutoML не вдалось виконати: {e.Message}");
        }
    }
    private static void RunCalibration(MLContext ml, Candidate best, List<TrainingRow> fitRows, List<TrainingRow> validRows, List<TrainingRow> testRows)
    {
        Console.WriteLine($"\n=== Калібрування ({best.Name}) ===");
        Console.WriteLine("Модель навчена на fit, калібратор — на valid (2018-04 – 2018-05), оцінка на test");
        ITransformer model = Build(ml, best).Fit(ml.Data.LoadFromEnumerable(fitRows));
        IDataView validScored = DropProbability(ml, model.Transform(ml.Data.LoadFromEnumerable(validRows)));
        ITransformer platt = ml.BinaryClassification.Calibrators.Platt(labelColumnName: "IsProblem").Fit(validScored);
        ITransformer isotonic = ml.BinaryClassification.Calibrators.Isotonic(labelColumnName: "IsProblem").Fit(validScored);
        Console.WriteLine("| Варіант | ROC-AUC | PR-AUC | LogLoss | Brier | ECE | Середня p |");
        Result? raw = Evaluate(ml, "без калібрування", model, testRows);
        Result? withPlatt = Evaluate(ml, "Platt", model, testRows, scored => platt.Transform(DropProbability(ml, scored)));
        Result? withIsotonic = Evaluate(ml, "Isotonic", model, testRows, scored => isotonic.Transform(DropProbability(ml, scored)));
        foreach (Result? result in new[] { raw, withPlatt, withIsotonic })
        {
            if (result is not null)
            {
                PrintRow(result);
            }
        }
        PrintBins(ml, "без калібрування", model, testRows, null);
        PrintBins(ml, "Platt", model, testRows, scored => platt.Transform(DropProbability(ml, scored)));
        PrintBins(ml, "Isotonic", model, testRows, scored => isotonic.Transform(DropProbability(ml, scored)));
    }
    private static IDataView DropProbability(MLContext ml, IDataView data)
    {
        return ml.Transforms.DropColumns("Probability").Fit(data).Transform(data);
    }
    private static IEstimator<ITransformer> Build(MLContext ml, Candidate candidate)
    {
        string[] numeric = candidate.Features.Numeric;
        string[] categorical = candidate.Features.Categorical;
        IEstimator<ITransformer> chain = new EstimatorChain<ITransformer>();
        if (numeric.Length > 0)
        {
            chain = chain.Append(ml.Transforms.ReplaceMissingValues(
                numeric.Select(n => new InputOutputColumnPair(n)).ToArray(),
                MissingValueReplacingEstimator.ReplacementMode.Mean));
        }
        if (categorical.Length > 0)
        {
            chain = chain.Append(ml.Transforms.Categorical.OneHotEncoding(
                categorical.Select(n => new InputOutputColumnPair(n + "Enc", n)).ToArray()));
        }
        chain = chain.Append(ml.Transforms.Concatenate("Features", numeric.Concat(categorical.Select(n => n + "Enc")).ToArray()));
        string featureColumn = "Features";
        if (candidate.MiSlots is int slots)
        {
            chain = chain.Append(ml.Transforms.FeatureSelection.SelectFeaturesBasedOnMutualInformation("FeaturesMi", "Features", slotsInOutput: slots, labelColumnName: "IsProblem"));
            featureColumn = "FeaturesMi";
        }
        return chain.Append(candidate.Trainer(ml, featureColumn));
    }
    private static TrainerFactory FastTree(int leaves, double learningRate, int trees)
    {
        return (ml, featureColumn) => ml.BinaryClassification.Trainers.FastTree(
            labelColumnName: "IsProblem", featureColumnName: featureColumn,
            numberOfLeaves: leaves, numberOfTrees: trees, learningRate: learningRate);
    }
    private static TrainerFactory LightGbm(int leaves, double learningRate, int iterations)
    {
        return (ml, featureColumn) => ml.BinaryClassification.Trainers.LightGbm(
            labelColumnName: "IsProblem", featureColumnName: featureColumn,
            numberOfLeaves: leaves, minimumExampleCountPerLeaf: 50, learningRate: learningRate, numberOfIterations: iterations);
    }
    private static Result? FitAndEvaluate(MLContext ml, Candidate candidate, List<TrainingRow> train, List<TrainingRow> test)
    {
        try
        {
            ITransformer model = Build(ml, candidate).Fit(ml.Data.LoadFromEnumerable(train));
            return Evaluate(ml, candidate.Name, model, test);
        }
        catch (Exception e)
        {
            Console.WriteLine($"  {candidate.Name}: помилка — {e.Message}");
            return null;
        }
    }
    private static Result? Evaluate(MLContext ml, string name, ITransformer model, List<TrainingRow> rows, Func<IDataView, IDataView>? post = null)
    {
        try
        {
            IDataView scored = model.Transform(ml.Data.LoadFromEnumerable(rows));
            if (post is not null)
            {
                scored = post(scored);
            }
            var metrics = ml.BinaryClassification.Evaluate(scored, labelColumnName: "IsProblem");
            List<ScoredRow> items = ml.Data.CreateEnumerable<ScoredRow>(scored, reuseRowObject: false).ToList();
            double brier = items.Average(x => Math.Pow(x.Probability - (x.IsProblem ? 1 : 0), 2));
            return new Result(name, metrics.AreaUnderRocCurve, metrics.AreaUnderPrecisionRecallCurve, metrics.LogLoss,
                brier, Ece(items), items.Average(x => x.Probability));
        }
        catch (Exception e)
        {
            Console.WriteLine($"  {name}: не вдалось оцінити — {e.Message}");
            return null;
        }
    }
    private static double Ece(List<ScoredRow> items)
    {
        const int bins = 10;
        List<ScoredRow> sorted = items.OrderBy(x => x.Probability).ToList();
        double total = 0;
        for (int b = 0; b < bins; b++)
        {
            int from = sorted.Count * b / bins;
            int to = sorted.Count * (b + 1) / bins;
            List<ScoredRow> chunk = sorted.GetRange(from, to - from);
            if (chunk.Count == 0)
            {
                continue;
            }
            double predicted = chunk.Average(x => x.Probability);
            double actual = chunk.Average(x => x.IsProblem ? 1.0 : 0.0);
            total += Math.Abs(predicted - actual) * chunk.Count;
        }
        return total / sorted.Count;
    }
    private static void PrintBins(MLContext ml, string name, ITransformer model, List<TrainingRow> rows, Func<IDataView, IDataView>? post)
    {
        IDataView scored = model.Transform(ml.Data.LoadFromEnumerable(rows));
        if (post is not null)
        {
            scored = post(scored);
        }
        List<ScoredRow> sorted = ml.Data.CreateEnumerable<ScoredRow>(scored, reuseRowObject: false).OrderBy(x => x.Probability).ToList();
        Console.WriteLine($"\nКалібрувальна таблиця ({name}): дециль | середня передбачена | фактична частка");
        for (int b = 0; b < 10; b++)
        {
            List<ScoredRow> chunk = sorted.GetRange(sorted.Count * b / 10, sorted.Count * (b + 1) / 10 - sorted.Count * b / 10);
            Console.WriteLine($"  {b + 1,2} | {chunk.Average(x => x.Probability).ToString("F4", CultureInfo.InvariantCulture)} | {chunk.Average(x => x.IsProblem ? 1.0 : 0.0).ToString("F4", CultureInfo.InvariantCulture)}");
        }
    }
    private static double Rate(List<TrainingRow> rows)
    {
        return rows.Count(r => r.IsProblem) / (double)rows.Count;
    }
    private static void PrintRow(Result r)
    {
        CultureInfo c = CultureInfo.InvariantCulture;
        Console.WriteLine($"| {r.Name} | {r.RocAuc.ToString("F4", c)} | {r.PrAuc.ToString("F4", c)} | {r.LogLoss.ToString("F4", c)} | {r.Brier.ToString("F4", c)} | {r.Ece.ToString("F4", c)} | {r.MeanProbability.ToString("F4", c)} |");
    }
}