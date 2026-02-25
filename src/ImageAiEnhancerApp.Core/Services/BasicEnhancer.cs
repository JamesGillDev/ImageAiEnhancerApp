using ImageAiEnhancerApp.Core.Interfaces;
using ImageAiEnhancerApp.Domain.Models;
using SkiaSharp;

namespace ImageAiEnhancerApp.Core.Services;

public sealed class BasicEnhancer : IBasicEnhancer
{
    public async Task<SKBitmap> EnhanceAsync(SKBitmap source, EnhanceOptions options, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(0d);

            var scale = Math.Clamp(options.Scale, 1, 4);
            var resized = ResizeWithHighQuality(source, source.Width * scale, source.Height * scale);
            progress?.Report(0.5d);

            if (options.Denoise)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var denoised = ApplyBlur(resized, 0.8f);
                resized.Dispose();
                resized = denoised;
            }

            if (options.Sharpen)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sharpened = ApplySharpen(resized);
                resized.Dispose();
                resized = sharpened;
            }

            progress?.Report(1d);
            return resized;
        }, cancellationToken).ConfigureAwait(false);
    }

    private static SKBitmap ResizeWithHighQuality(SKBitmap source, int width, int height)
    {
        var output = new SKBitmap(width, height, source.ColorType, source.AlphaType);
        using var canvas = new SKCanvas(output);
        using var paint = new SKPaint
        {
            FilterQuality = SKFilterQuality.High,
            IsAntialias = true,
            IsDither = true
        };

        canvas.Clear(SKColors.Transparent);
        canvas.DrawBitmap(source, new SKRect(0, 0, width, height), paint);
        canvas.Flush();
        return output;
    }

    private static SKBitmap ApplyBlur(SKBitmap source, float sigma)
    {
        using var image = SKImage.FromBitmap(source);
        using var filter = SKImageFilter.CreateBlur(sigma, sigma);
        using var paint = new SKPaint { ImageFilter = filter };
        using var surface = SKSurface.Create(new SKImageInfo(source.Width, source.Height, source.ColorType, source.AlphaType));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.DrawImage(image, 0, 0, paint);
        canvas.Flush();

        using var snapshot = surface.Snapshot();
        return SKBitmap.FromImage(snapshot);
    }

    private static SKBitmap ApplySharpen(SKBitmap source)
    {
        var kernel = new float[]
        {
             0, -1,  0,
            -1,  5, -1,
             0, -1,  0
        };

        using var filter = SKImageFilter.CreateMatrixConvolution(
            new SKSizeI(3, 3),
            kernel,
            gain: 1f,
            bias: 0f,
            kernelOffset: new SKPointI(1, 1),
            tileMode: SKShaderTileMode.Clamp,
            convolveAlpha: true);

        using var image = SKImage.FromBitmap(source);
        using var paint = new SKPaint { ImageFilter = filter };
        using var surface = SKSurface.Create(new SKImageInfo(source.Width, source.Height, source.ColorType, source.AlphaType));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.DrawImage(image, 0, 0, paint);
        canvas.Flush();

        using var snapshot = surface.Snapshot();
        return SKBitmap.FromImage(snapshot);
    }
}
