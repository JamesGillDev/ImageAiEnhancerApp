using System.Globalization;
using ImageAiEnhancerApp.Core.Interfaces;
using ImageAiEnhancerApp.Domain.Models;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace ImageAiEnhancerApp.Core.Services;

public sealed class OnnxUpscaleEnhancer : IOnnxUpscaleEnhancer
{
    private const int ModelInputSize = 224;
    private readonly IAppLogger _logger;

    public OnnxUpscaleEnhancer(IAppLogger logger)
    {
        _logger = logger;
    }

    public bool IsDirectMlAvailable()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            using var options = new SessionOptions();
            options.AppendExecutionProvider_DML(0);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public string GetActiveProviderName(bool useGpuIfAvailable)
    {
        return useGpuIfAvailable && IsDirectMlAvailable() ? "DirectML" : "CPU";
    }

    public async Task<SKBitmap> EnhanceAsync(SKBitmap source, ModelDescriptor model, EnhanceOptions options, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        return await Task.Run(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(0d);

            if (!File.Exists(model.Path))
            {
                throw new FileNotFoundException("Model file not found.", model.Path);
            }

            using var sessionOptions = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
            };

            var provider = "CPU";
            if (options.UseGpuIfAvailable && IsDirectMlAvailable())
            {
                sessionOptions.AppendExecutionProvider_DML(0);
                provider = "DirectML";
            }

            await _logger.LogAsync($"ONNX provider selected: {provider}", cancellationToken).ConfigureAwait(false);
            using var session = new InferenceSession(model.Path, sessionOptions);

            var inputName = session.InputMetadata.Keys.First();
            var outputName = session.OutputMetadata.Keys.First();
            var inputShape = string.Join("x", session.InputMetadata[inputName].Dimensions.Select(d => d.ToString(CultureInfo.InvariantCulture)));
            var outputShape = string.Join("x", session.OutputMetadata[outputName].Dimensions.Select(d => d.ToString(CultureInfo.InvariantCulture)));

            await _logger.LogAsync($"ONNX model '{model.Name}' loaded. Input='{inputName}' shape={inputShape}; Output='{outputName}' shape={outputShape}.", cancellationToken)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            using var resizedInput = ResizeWithHighQuality(source, ModelInputSize, ModelInputSize);
            var ycbcr = ExtractYcbcr(resizedInput);
            var inputTensor = new DenseTensor<float>(ycbcr.Y, new[] { 1, 1, ModelInputSize, ModelInputSize });

            progress?.Report(0.25d);

            var inputValue = NamedOnnxValue.CreateFromTensor(inputName, inputTensor);
            using var results = session.Run(new[] { inputValue }, new[] { outputName });

            cancellationToken.ThrowIfCancellationRequested();

            var outputTensor = results.First().AsTensor<float>();
            progress?.Report(0.5d);

            var outputWidth = (int)outputTensor.Dimensions[3];
            var outputHeight = (int)outputTensor.Dimensions[2];
            var yUpscaled = TensorToLuma(outputTensor, outputWidth, outputHeight);

            using var cbUpscaled = ResizeChannel(ycbcr.Cb, outputWidth, outputHeight);
            using var crUpscaled = ResizeChannel(ycbcr.Cr, outputWidth, outputHeight);

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(0.75d);

            var merged = MergeToRgb(yUpscaled, cbUpscaled, crUpscaled, outputWidth, outputHeight);
            progress?.Report(1d);
            return merged;
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

        canvas.Clear(SKColors.Black);
        canvas.DrawBitmap(source, new SKRect(0, 0, width, height), paint);
        canvas.Flush();
        return output;
    }

    private static (float[] Y, SKBitmap Cb, SKBitmap Cr) ExtractYcbcr(SKBitmap bitmap)
    {
        var y = new float[ModelInputSize * ModelInputSize];
        var cb = new SKBitmap(ModelInputSize, ModelInputSize, SKColorType.Gray8, SKAlphaType.Opaque);
        var cr = new SKBitmap(ModelInputSize, ModelInputSize, SKColorType.Gray8, SKAlphaType.Opaque);

        for (var py = 0; py < ModelInputSize; py++)
        {
            for (var px = 0; px < ModelInputSize; px++)
            {
                var color = bitmap.GetPixel(px, py);
                var r = color.Red / 255f;
                var g = color.Green / 255f;
                var b = color.Blue / 255f;

                var luma = (0.299f * r) + (0.587f * g) + (0.114f * b);
                var cbValue = (-0.168736f * r) - (0.331264f * g) + (0.5f * b) + 0.5f;
                var crValue = (0.5f * r) - (0.418688f * g) - (0.081312f * b) + 0.5f;

                var index = py * ModelInputSize + px;
                y[index] = Clamp01(luma);
                cb.SetPixel(px, py, ToGray(cbValue));
                cr.SetPixel(px, py, ToGray(crValue));
            }
        }

        return (y, cb, cr);
    }

    private static float[] TensorToLuma(Tensor<float> outputTensor, int width, int height)
    {
        var luma = new float[width * height];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var value = outputTensor[0, 0, y, x];
                luma[(y * width) + x] = Clamp01(value);
            }
        }

        return luma;
    }

    private static SKBitmap ResizeChannel(SKBitmap source, int dstWidth, int dstHeight)
    {
        var output = new SKBitmap(dstWidth, dstHeight, SKColorType.Gray8, SKAlphaType.Opaque);
        using var canvas = new SKCanvas(output);
        using var paint = new SKPaint
        {
            FilterQuality = SKFilterQuality.High,
            IsDither = true,
            IsAntialias = true
        };

        canvas.Clear(SKColors.Black);
        canvas.DrawBitmap(source, new SKRect(0, 0, dstWidth, dstHeight), paint);
        canvas.Flush();
        return output;
    }

    private static SKBitmap MergeToRgb(float[] yChannel, SKBitmap cbChannel, SKBitmap crChannel, int width, int height)
    {
        var output = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);

        for (var py = 0; py < height; py++)
        {
            for (var px = 0; px < width; px++)
            {
                var index = py * width + px;
                var y = yChannel[index];
                var cb = cbChannel.GetPixel(px, py).Red / 255f;
                var cr = crChannel.GetPixel(px, py).Red / 255f;

                var r = y + (1.402f * (cr - 0.5f));
                var g = y - (0.344136f * (cb - 0.5f)) - (0.714136f * (cr - 0.5f));
                var b = y + (1.772f * (cb - 0.5f));

                output.SetPixel(px, py, new SKColor(
                    (byte)(Clamp01(r) * 255f),
                    (byte)(Clamp01(g) * 255f),
                    (byte)(Clamp01(b) * 255f),
                    255));
            }
        }

        return output;
    }

    private static SKColor ToGray(float value)
    {
        var clamped = (byte)(Clamp01(value) * 255f);
        return new SKColor(clamped, clamped, clamped, 255);
    }

    private static float Clamp01(float value)
    {
        if (value < 0f)
        {
            return 0f;
        }

        if (value > 1f)
        {
            return 1f;
        }

        return value;
    }
}
