using ImageAiEnhancerApp.Domain.Models;

namespace ImageAiEnhancerApp.Core.Interfaces;

public interface IJobRunner
{
    string BuildOutputPath(string inputPath, string outputFolder, string suffix, string outputFormat);
    Task RunJobAsync(EnhanceJob job, IProgress<double>? progress, CancellationToken cancellationToken);
    Task<string> RunBatchAsync(IList<EnhanceJob> jobs, IProgress<EnhanceJob>? progress, CancellationToken cancellationToken);
}
