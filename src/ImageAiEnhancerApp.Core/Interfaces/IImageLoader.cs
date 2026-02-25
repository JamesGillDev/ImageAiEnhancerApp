using SkiaSharp;

namespace ImageAiEnhancerApp.Core.Interfaces;

public interface IImageLoader
{
    SKBitmap LoadBitmap(string path);
    Task SaveBitmapAsync(SKBitmap bitmap, string outputPath, string outputFormat, int jpegQuality, CancellationToken cancellationToken);
    bool IsSupportedImagePath(string path);
}
