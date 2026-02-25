using ImageAiEnhancerApp.Domain.Models;
using SkiaSharp;

namespace ImageAiEnhancerApp.Core.Interfaces;

public interface IOnnxUpscaleEnhancer
{
    bool IsDirectMlAvailable();
    string GetActiveProviderName(bool useGpuIfAvailable);
    Task<SKBitmap> EnhanceAsync(SKBitmap source, ModelDescriptor model, EnhanceOptions options, IProgress<double>? progress, CancellationToken cancellationToken);
}
