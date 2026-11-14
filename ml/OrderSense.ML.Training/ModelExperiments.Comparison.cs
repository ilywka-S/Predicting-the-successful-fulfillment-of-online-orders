using System.Globalization;
using Microsoft.ML;
namespace OrderSense.ML.Training;
public static partial class ModelExperiments
{
    public static void RunComparison(MLContext ml, List<TrainingRow> allRows)
    {
        FeatureSet all = new FeatureSet("усі 12", Numeric, Categorical);
        FeatureSet reduced = new FeatureSet("10 ознак", Numeric.Where(n => n != "PurchaseDayOfWeek").ToArray(), Categorical.Where(n => n != "MainCategory").ToArray());
        List<Candidate> candidates =
        [
            new Candidate("v1 FastTree l=20 lr=0.2 t=100", all, FastTree(20, 0.2, 100)),
            new Candidate("FastTree l=8 lr=0.05 t=300", all, FastTree(8, 0.05, 300)),
            new Candidate("FastTree l=8 lr=0.05 t=300, 10 ознак", reduced, FastTree(8, 0.05, 300)),
            new Candidate("LightGBM l=8 lr=0.03 it=200", all, LightGbm(8, 0.03, 200)),
        ];
        (string Label, DateTime CalibStart, DateTime TestStart, DateTime TestEnd)[] folds =
        [
            ("A тест 2018-01–03", new DateTime(2017, 11, 1), new DateTime(2018, 1, 1), new DateTime(2018, 4, 1)),
            ("B тест 2018-04–06", new DateTime(2018, 2, 1), new DateTime(2018, 4, 1), new DateTime(2018, 7, 1)),
            ("C тест 2018-06–08", new DateTime(2018, 4, 1), new DateTime(2018, 6, 1), new DateTime(2018, 9, 1)),
        ];
        List<(string Name, float[] P, bool[] Y)> foldC = [];
        Console.WriteLine("\n=== Чесне порівняння: fit -> Platt на calib -> тест (однаково для всіх) ===");
        Console.WriteLine("| Вікно · модель · варіант | ROC-AUC | PR-AUC | LogLoss | Brier | ECE | Середня p |");
        foreach ((string label, DateTime calibStart, DateTime testStart, DateTime testEnd) in folds)
        {
            List<TrainingRow> fit = allRows.Where(r => r.PurchasedAt < calibStart).ToList();
            List<TrainingRow> calib = allRows.Where(r => r.PurchasedAt >= calibStart && r.PurchasedAt < testStart).ToList();
            List<TrainingRow> test = allRows.Where(r => r.PurchasedAt >= testStart && r.PurchasedAt < testEnd).ToList();
            Console.WriteLine($"\n# {label}: fit {fit.Count}, calib {calib.Count}, test {test.Count}, частка проблем у тесті {Rate(test).ToString("P2", CultureInfo.InvariantCulture)}");
            foreach (Candidate candidate in candidates)
            {
                try
                {
                    ITransformer model = Build(ml, candidate).Fit(ml.Data.LoadFromEnumerable(fit));
                    IDataView calibScored = DropProbability(ml, model.Transform(ml.Data.LoadFromEnumerable(calib)));
                    ITransformer platt = ml.BinaryClassification.Calibrators.Platt(labelColumnName: "IsProblem").Fit(calibScored);
                    Result? raw = Evaluate(ml, $"{label} · {candidate.Name} · raw", model, test);
                    Result? calibrated = Evaluate(ml, $"{label} · {candidate.Name} · Platt", model, test, scored => platt.Transform(DropProbability(ml, scored)));
                    if (raw is not null)
                    {
                        PrintRow(raw);
                    }
                    if (calibrated is not null)
                    {
                        PrintRow(calibrated);
                    }
                    if (label.StartsWith("C"))
                    {
                        (float[] p, bool[] y) = Scores(ml, model, test);
                        foldC.Add((candidate.Name, p, y));
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"  {label} · {candidate.Name}: помилка — {e.Message}");
                }
            }
        }
        if (foldC.Count > 1)
        {
            Bootstrap(foldC);
        }
    }
    private static (float[] P, bool[] Y) Scores(MLContext ml, ITransformer model, List<TrainingRow> rows)
    {
        List<ScoredRow> items = ml.Data.CreateEnumerable<ScoredRow>(model.Transform(ml.Data.LoadFromEnumerable(rows)), reuseRowObject: false).ToList();
        return (items.Select(x => x.Probability).ToArray(), items.Select(x => x.IsProblem).ToArray());
    }
    private static void Bootstrap(List<(string Name, float[] P, bool[] Y)> models)
    {
        const int resamples = 300;
        Random random = new Random(42);
        int n = models[0].Y.Length;
        List<List<double>> roc = models.Select(_ => new List<double>()).ToList();
        List<List<double>> ap = models.Select(_ => new List<double>()).ToList();
        for (int r = 0; r < resamples; r++)
        {
            int[] sample = Enumerable.Range(0, n).Select(_ => random.Next(n)).ToArray();
            for (int m = 0; m < models.Count; m++)
            {
                roc[m].Add(RocAuc(models[m].P, models[m].Y, sample));
                ap[m].Add(AveragePrecision(models[m].P, models[m].Y, sample));
            }
        }
        CultureInfo c = CultureInfo.InvariantCulture;
        Console.WriteLine("\n=== Бутстреп на вікні C (300 повторів, 95% інтервал; AP — середня точність, близька до PR-AUC) ===");
        for (int m = 0; m < models.Count; m++)
        {
            Console.WriteLine($"{models[m].Name}: ROC {Mean(roc[m]).ToString("F4", c)} [{Percentile(roc[m], 0.025).ToString("F4", c)}; {Percentile(roc[m], 0.975).ToString("F4", c)}], " + $"AP {Mean(ap[m]).ToString("F4", c)} [{Percentile(ap[m], 0.025).ToString("F4", c)}; {Percentile(ap[m], 0.975).ToString("F4", c)}]");
        }
        Console.WriteLine("\nРізниця з v1 (парний бутстреп): Δ [95% інтервал], частка повторів, де модель краща за v1");
        for (int m = 1; m < models.Count; m++)
        {
            List<double> dRoc = roc[m].Select((v, i) => v - roc[0][i]).ToList();
            List<double> dAp = ap[m].Select((v, i) => v - ap[0][i]).ToList();
            Console.WriteLine($"{models[m].Name}: ΔROC {Mean(dRoc).ToString("F4", c)} [{Percentile(dRoc, 0.025).ToString("F4", c)}; {Percentile(dRoc, 0.975).ToString("F4", c)}] краща у {dRoc.Count(x => x > 0) * 100.0 / resamples:F0}%, " + $"ΔAP {Mean(dAp).ToString("F4", c)} [{Percentile(dAp, 0.025).ToString("F4", c)}; {Percentile(dAp, 0.975).ToString("F4", c)}] краща у {dAp.Count(x => x > 0) * 100.0 / resamples:F0}%");
        }
    }
    private static double Mean(List<double> values)
    {
        return values.Average();
    }
    private static double Percentile(List<double> values, double q)
    {
        List<double> sorted = values.OrderBy(v => v).ToList();
        return sorted[Math.Min(sorted.Count - 1, (int)(q * sorted.Count))];
    }
    private static double RocAuc(float[] p, bool[] y, int[] sample)
    {
        int[] order = sample.OrderBy(i => p[i]).ToArray();
        double rankSumPositive = 0;
        long positives = 0;
        int start = 0;
        while (start < order.Length)
        {
            int end = start;
            while (end + 1 < order.Length && p[order[end + 1]] == p[order[start]])
            {
                end++;
            }
            double averageRank = (start + end) / 2.0 + 1;
            for (int k = start; k <= end; k++)
            {
                if (y[order[k]])
                {
                    rankSumPositive += averageRank;
                    positives++;
                }
            }
            start = end + 1;
        }
        long negatives = order.Length - positives;
        if (positives == 0 || negatives == 0)
        {
            return 0.5;
        }
        return (rankSumPositive - positives * (positives + 1) / 2.0) / (positives * (double)negatives);
    }
    private static double AveragePrecision(float[] p, bool[] y, int[] sample)
    {
        int[] order = sample.OrderByDescending(i => p[i]).ToArray();
        long totalPositives = order.Count(i => y[i]);
        if (totalPositives == 0)
        {
            return 0;
        }
        double result = 0;
        long truePositives = 0;
        long seen = 0;
        int start = 0;
        while (start < order.Length)
        {
            int end = start;
            while (end + 1 < order.Length && p[order[end + 1]] == p[order[start]])
            {
                end++;
            }
            long groupPositives = 0;
            for (int k = start; k <= end; k++)
            {
                if (y[order[k]])
                {
                    groupPositives++;
                }
            }
            seen += end - start + 1;
            truePositives += groupPositives;
            result += groupPositives / (double)totalPositives * (truePositives / (double)seen);
            start = end + 1;
        }
        return result;
    }
}