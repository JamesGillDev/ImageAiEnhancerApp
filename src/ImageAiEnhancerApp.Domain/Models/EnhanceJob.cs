using ImageAiEnhancerApp.Domain.Enums;

namespace ImageAiEnhancerApp.Domain.Models;

public class EnhanceJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string InputPath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public EnhanceOptions Options { get; set; } = new();
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public double Progress { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? StartedUtc { get; set; }
    public DateTime? FinishedUtc { get; set; }
}