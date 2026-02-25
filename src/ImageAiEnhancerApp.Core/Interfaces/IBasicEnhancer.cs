using ImageAiEnhancerApp.Domain.Models;
using SkiaSharp;

namespace ImageAiEnhancerApp.Core.Interfaces;

public interface IBasicEnhancer
{
    Task<SKBitmap> EnhanceAsync(SKBitmap source, EnhanceOptions options, IProgress<double>? progress, CancellationToken cancellationToken);
}
