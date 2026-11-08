using OrderSense.Client.Core.Interfaces;

namespace OrderSense.Client.Core.Services.Mock;

public class MockAnalyticsService : IAnalyticsService
{
    private static readonly DateTime FirstDay = new(2018, 1, 1);
    private static readonly DateTime LastDay = new(2018, 8, 31);

    private static readonly (string Key, int Orders, double Risky)[] Categories =
    [
        ("bed_bath_table", 1820, 0.27),
        ("health_beauty", 1710, 0.19),
        ("sports_leisure", 1460, 0.22),
        ("furniture_decor", 1290, 0.31),
        ("computers_accessories", 1180, 0.25),
        ("housewares", 1060, 0.24),
        ("watches_gifts", 980, 0.18),
        ("telephony", 760, 0.21),
        ("garden_tools", 690, 0.28),
        ("auto", 640, 0.23),
    ];

    private static readonly (string Key, int Orders, double Risky)[] States =
    [
        ("SP", 6150, 0.14),
        ("RJ", 1890, 0.33),
        ("MG", 1720, 0.22),
        ("RS", 810, 0.26),
        ("PR", 760, 0.20),
        ("SC", 540, 0.27),
        ("BA", 510, 0.38),
        ("DF", 330, 0.24),
        ("GO", 300, 0.29),
        ("ES", 290, 0.34),
    ];

    private static readonly (string Key, int Orders, double Risky)[] Sellers =
    [
        ("4a3ca9315b744ce9f8e9374361493884", 410, 0.21),
        ("6560211a19b47992c3666cc44a7e94c0", 380, 0.17),
        ("1f50f920176fa81dab994f9023523100", 350, 0.29),
        ("cc419e0650a3c5ba77189a1882b7556a", 320, 0.24),
        ("da8622b14eb17ae2831f4ac5b9dab84a", 300, 0.33),
        ("955fee9216a65b617aa5c0531780ce60", 280, 0.19),
        ("1025f0e2d44d7041d6cf58b6550e0bfa", 260, 0.27),
        ("7c67e1448b00f6e969d365cea6b010ab", 240, 0.36),
        ("ea8482cd71df3c1969d7b9473ff13abc", 220, 0.22),
        ("7a67c85e85bb2ce8582c35f2203ad736", 200, 0.31),
    ];

    public async Task<IReadOnlyList<AnalyticsPointDto>> GetRiskAsync(
        AnalyticsDimension dimension,
        DateTime? fromDate,
        DateTime? toDate,
        int top,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(300, cancellationToken);

        var days = Days(fromDate, toDate);
        if (dimension == AnalyticsDimension.Day)
        {
            return days;
        }

        var totalDays = (LastDay - FirstDay).Days + 1;
        var ratio = (double)days.Count / totalDays;
        var source = dimension switch
        {
            AnalyticsDimension.Category => Categories,
            AnalyticsDimension.State => States,
            _ => Sellers,
        };

        return source
            .Take(top)
            .Select(row => Point(row.Key, (int)Math.Round(row.Orders * ratio), row.Risky))
            .Where(point => point.Orders > 0)
            .ToList();
    }

    private static List<AnalyticsPointDto> Days(DateTime? from, DateTime? to)
    {
        var start = from?.Date > FirstDay ? from.Value.Date : FirstDay;
        var end = to?.Date < LastDay ? to.Value.Date : LastDay;
        var random = new Random(42);
        var points = new List<AnalyticsPointDto>();

        for (var day = FirstDay; day <= LastDay; day = day.AddDays(1))
        {
            var index = (day - FirstDay).Days;
            var weekend = day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            var orders = (weekend ? 45 : 70) + random.Next(0, 30);
            var risky = 0.22 + (0.08 * Math.Sin(index / 18.0)) + ((random.NextDouble() - 0.5) * 0.06);

            if (day >= start && day <= end)
            {
                points.Add(Point(day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), orders, risky));
            }
        }

        return points;
    }

    private static AnalyticsPointDto Point(string key, int orders, double risky)
    {
        var predicted = (int)Math.Round(orders * 0.96);
        var high = (int)Math.Round(predicted * risky * 0.4);
        var medium = (int)Math.Round(predicted * risky) - high;

        return new AnalyticsPointDto
        {
            Key = key,
            Orders = orders,
            Predicted = predicted,
            Low = predicted - medium - high,
            Medium = medium,
            High = high,
            RiskyShare = predicted > 0 ? (double)(medium + high) / predicted : null,
        };
    }
}
