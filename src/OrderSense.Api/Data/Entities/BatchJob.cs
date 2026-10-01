using System.ComponentModel.DataAnnotations;

namespace OrderSense.Api.Data.Entities;

public class BatchJob
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public BatchJobStatus Status { get; set; }

    [MaxLength(255)]
    public required string FileName { get; set; }

    public int TotalRows { get; set; }

    public int ProcessedRows { get; set; }

    public string? Error { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public List<BatchResult> Results { get; set; } = [];
}