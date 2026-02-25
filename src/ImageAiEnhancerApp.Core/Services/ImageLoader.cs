using ImageAiEnhancerApp.Core.Interfaces;
using SkiaSharp;

namespace ImageAiEnhancerApp.Core.Services;

public sealed class ImageLoader : IImageLoader
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".tif",
        ".tiff"
    };

    public SKBitmap LoadBitmap(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Input image was not found.", path);
        }

        using var stream = File.OpenRead(path);
        var bitmap = SKBitmap.Decode(stream);
        if (bitmap is null)
        {
            throw new InvalidOperationException("Unsupported or corrupted image format.");
        }

        return bitmap;
    }

    public async Task SaveBitmapAsync(SKBitmap bitmap, string outputPath, string outputFormat, int jpegQuality, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var extension = NormalizeFormat(outputFormat);
        var format = extension.Equals("jpg", StringComparison.OrdinalIgnoreCase)
            ? SKEncodedImageFormat.Jpeg
            : SKEncodedImageFormat.Png;

        var quality = Math.Clamp(jpegQuality, 1, 100);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? string.Empty);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, quality);
        if (data is null)
        {
            throw new InvalidOperationException("Failed to encode output image.");
        }

        await using var file = File.Open(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await data.AsStream().CopyToAsync(file, cancellationToken).ConfigureAwait(false);
    }

    public bool IsSupportedImagePath(string path)
    {
        var extension = Path.GetExtension(path);
        return SupportedExtensions.Contains(extension);
    }

    private static string NormalizeFormat(string outputFormat)
    {
        if (string.Equals(outputFormat, "jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return "jpg";
        }

        return string.Equals(outputFormat, "jpg", StringComparison.OrdinalIgnoreCase)
            ? "jpg"
            : "png";
    }
}
