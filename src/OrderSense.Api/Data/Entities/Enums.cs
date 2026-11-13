namespace OrderSense.Api.Data.Entities;

public enum RiskLevel
{
    Low,
    Medium,
    High,
}

public enum BatchJobStatus
{
    Queued,
    Running,
    Done,
    Failed,
}
public enum BatchJobKind
{
    Upload,
    PredictMissing,
}
public enum DevicePlatform
{
    Android,
    Ios,
    Windows,
    MacOs,
}