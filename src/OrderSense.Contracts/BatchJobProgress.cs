namespace OrderSense.Contracts;
public record BatchJobProgress(Guid JobId, int ProcessedRows, int TotalRows, string Status);