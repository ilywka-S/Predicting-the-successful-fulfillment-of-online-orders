using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using OrderSense.Contracts;
namespace OrderSense.Api.Tests.Infrastructure;
public sealed class FakePredictionService : HttpMessageHandler
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };
    private bool _failNextCall;
    public OrderPrediction Result 
    { 
        get; 
        set; 
    } = new() { Probability = 0.9f, RiskLevel = "low", ModelVersion = "v-test" };
    public ModelDetails? Model 
    { 
        get; 
        set; 
    } = CreateModel();
    public OrderExplanation Explanation 
    { 
        get; 
        set; 
    } = CreateExplanation();
    public bool FailExplain 
    { 
        get; 
        set; 
    }
    public OrderFeatures? LastFeatures 
    { 
        get; 
        private set; 
    }
    public string? LastPath 
    { 
        get; 
        private set; 
    }
    public int Calls 
    { 
        get; 
        private set; 
    }
    public int ExplainCalls 
    { 
        get; 
        private set; 
    }
    public int BatchCalls 
    { 
        get; 
        private set; 
    }
    public bool RejectBatches 
    { 
        get; 
        set; 
    }

    public int? SucceedSinglesCount 
    { 
        get; 
        set; 
    }
    public Func<Task>? BeforeBatch 
    { 
        get; 
        set; 
    }
    private int _singleCalls;
    public void FailNextCall() => _failNextCall = true;
    public void Reset()
    {
        _failNextCall = false;
        FailExplain = false;
        RejectBatches = false;
        SucceedSinglesCount = null;
        BeforeBatch = null;
        _singleCalls = 0;
        Result = new OrderPrediction { Probability = 0.9f, RiskLevel = "low", ModelVersion = "v-test" };
        Model = CreateModel();
        Explanation = CreateExplanation();
    }
    public static OrderExplanation CreateExplanation()
    {
        return new OrderExplanation
        {
            ModelVersion = "v-test",
            Factors =
            [
                new FeatureContribution { Feature = "PromisedDays", Contribution = 0.20f },
                new FeatureContribution { Feature = "ItemsCount", Contribution = -0.05f },
            ],
        };
    }
    public static ModelDetails CreateModel()
    {
        return new ModelDetails
        {
            Version = "v-test",
            TrainedAt = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero),
            TrainRows = 1000,
            TestRows = 200,
            TrainPeriod = "2017-01 – 2018-05",
            TestPeriod = "2018-06 – 2018-08",
            RocAuc = 0.61f,
            PrAuc = 0.07f,
            F1 = 0.06f,
            BaselinePrAuc = 0.04f,
            RiskMediumFrom = 0.12f,
            RiskHighFrom = 0.20f,
            FeatureImportances =
            [
                new FeatureImportance { Feature = "PromisedDays", Importance = 0.028f },
                new FeatureImportance { Feature = "DistanceKm", Importance = -0.004f },
            ],
        };
    }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string path = request.RequestUri?.AbsolutePath ?? "";
        if (path == "/explain")
        {
            ExplainCalls++;
            if (FailExplain)
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Explanation, options: Json) };
        }
        Calls++;
        LastPath = path;
        if (_failNextCall)
        {
            _failNextCall = false;
            throw new HttpRequestException("Prediction service is down");
        }
        if (path == "/predict/batch" && RejectBatches)
        {
            return new HttpResponseMessage(HttpStatusCode.BadRequest);
        }
        if (path == "/predict")
        {
            _singleCalls++;

            if (SucceedSinglesCount is { } limit && _singleCalls > limit)
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }
        }
        if (request.Method == HttpMethod.Get && path == "/model")
        {
            if (Model == null)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Model, options: Json) };
        }
        if (path == "/predict/batch")
        {
            BatchCalls++;
            if (BeforeBatch is { } beforeBatch)
            {
                await beforeBatch();
            }
            List<OrderFeatures>? batch = await request.Content!.ReadFromJsonAsync<List<OrderFeatures>>(Json, cancellationToken);
            List<OrderPrediction> list = (batch ?? []).Select(_ => Result).ToList();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(list, options: Json) };
        }
        LastFeatures = await request.Content!.ReadFromJsonAsync<OrderFeatures>(Json, cancellationToken);
        
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Result, options: Json) };
    }
}