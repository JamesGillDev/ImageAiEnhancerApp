using System.Text;
using ImageAiEnhancerApp.Core.Interfaces;
using ImageAiEnhancerApp.Domain.Enums;
using ImageAiEnhancerApp.Domain.Models;
using SkiaSharp;

namespace ImageAiEnhancerApp.Core.Services;

public sealed class JobRunner : IJobRunner
{
    private readonly IImageLoader _imageLoader;
    private readonly IBasicEnhancer _basicEnhancer;
    private readonly IOnnxUpscaleEnhancer _onnxEnhancer;
    private readonly IModelManager _modelManager;
    private readonly AppDataPaths _paths;
    private readonly IAppLogger _logger;

    public JobRunner(
        IImageLoader imageLoader,
        IBasicEnhancer basicEnhancer,
        IOnnxUpscaleEnhancer onnxEnhancer,
        IModelManager modelManager,
        AppDataPaths paths,
        IAppLogger logger)
    {
        _imageLoader = imageLoader;
        _basicEnhancer = basicEnhancer;
        _onnxEnhancer = onnxEnhancer;
        _modelManager = modelManager;
        _paths = paths;
        _logger = logger;
    }

    public string BuildOutputPath(string inputPath, string outputFolder, string suffix, string outputFormat)
    {
        if (string.IsNullOrWhiteSpace(outputFolder))
        {
            outputFolder = _paths.OutputDirectory;
        }

        Directory.CreateDirectory(outputFolder);

        var inputDirectory = Path.GetDirectoryName(inputPath) ?? string.Empty;
        var sameFolder = string.Equals(
            Path.GetFullPath(inputDirectory),
            Path.GetFullPath(outputFolder),
            StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(suffix) || (sameFolder && string.IsNullOrWhiteSpace(suffix)))
        {
            suffix = "_enhanced";
        }

        var extension = string.Equals(outputFormat, "jpg", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(outputFormat, "jpeg", StringComparison.OrdinalIgnoreCase)
            ? ".jpg"
            : ".png";

        var baseName = Path.GetFileNameWithoutExtension(inputPath);
        var candidate = Path.Combine(outputFolder, $"{baseName}{suffix}{extension}");
        var counter = 1;

        while (File.Exists(candidate))
        {
            candidate = Path.Combine(outputFolder, $"{baseName}{suffix}_{counter}{extension}");
            counter++;
        }

        return candidate;
    }

    public async Task RunJobAsync(EnhanceJob job, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        job.Status = JobStatus.Running;
        job.StartedUtc = DateTime.UtcNow;
        job.FinishedUtc = null;
        job.ErrorMessage = null;
        job.Progress = 0;

        try
        {
            await _logger.LogAsync($"Job {job.Id} started. Input='{job.InputPath}' Output='{job.OutputPath}' Mode={job.Options.Mode}", cancellationToken)
                .ConfigureAwait(false);

            using var inputBitmap = _imageLoader.LoadBitmap(job.InputPath);
            SKBitmap? resultBitmap = null;

            if (job.Options.Mode == EnhanceMode.Basic)
            {
                resultBitmap = await _basicEnhancer.EnhanceAsync(
                    inputBitmap,
                    job.Options,
                    new Progress<double>(p =>
                    {
                        job.Progress = Math.Clamp(p * 0.8d, 0d, 0.8d);
                        progress?.Report(job.Progress);
                    }),
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var model = _modelManager.FindByName(job.Options.SelectedModelName);
                if (model is null)
                {
                    throw new InvalidOperationException("Selected AI model was not found in Models/models.json.");
                }

                var resolvedPath = _modelManager.ResolveModelPath(model);
                if (!File.Exists(resolvedPath))
                {
                    throw new FileNotFoundException($"Model file was not found at '{resolvedPath}'.");
                }

                var resolvedModel = new ModelDescriptor
                {
                    Name = model.Name,
                    Path = resolvedPath,
                    Scale = model.Scale,
                    InputName = model.InputName,
                    OutputName = model.OutputName,
                    Notes = model.Notes
                };

                resultBitmap = await _onnxEnhancer.EnhanceAsync(
                    inputBitmap,
                    resolvedModel,
                    job.Options,
                    new Progress<double>(p =>
                    {
                        job.Progress = Math.Clamp(0.1d + (p * 0.75d), 0d, 0.85d);
                        progress?.Report(job.Progress);
                    }),
                    cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            job.Progress = 0.9d;
            progress?.Report(job.Progress);

            using (resultBitmap)
            {
                await _imageLoader
                    .SaveBitmapAsync(resultBitmap, job.OutputPath, job.Options.OutputFormat, job.Options.JpegQuality, cancellationToken)
                    .ConfigureAwait(false);
            }

            job.Progress = 1d;
            job.Status = JobStatus.Done;
            await _logger.LogAsync($"Job {job.Id} completed successfully.", cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            job.Status = JobStatus.Canceled;
            job.ErrorMessage = "Canceled";
            await _logger.LogAsync($"Job {job.Id} canceled.").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            job.Status = JobStatus.Failed;
            job.ErrorMessage = ex.Message;
            await _logger.LogAsync($"Job {job.Id} failed: {ex.Message}").ConfigureAwait(false);
        }
        finally
        {
            job.FinishedUtc = DateTime.UtcNow;
            progress?.Report(job.Progress);
        }
    }

    public async Task<string> RunBatchAsync(IList<EnhanceJob> jobs, IProgress<EnhanceJob>? progress, CancellationToken cancellationToken)
    {
        _paths.EnsureCreated();
        var logPath = Path.Combine(_paths.LogsDirectory, $"batch_{DateTime.UtcNow:yyyyMMdd_HHmmss}.log");

        await using var stream = File.Create(logPath);
        await using var writer = new StreamWriter(stream, Encoding.UTF8);

        await writer.WriteLineAsync($"Batch started (UTC): {DateTime.UtcNow:O}");
        await writer.WriteLineAsync($"Jobs: {jobs.Count}");
        await writer.WriteLineAsync("-");
        await writer.FlushAsync();

        foreach (var job in jobs)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                if (job.Status == JobStatus.Pending)
                {
                    job.Status = JobStatus.Canceled;
                    job.ErrorMessage = "Canceled before start";
                    progress?.Report(job);
                }

                continue;
            }

            await RunJobAsync(
                job,
                new Progress<double>(_ => progress?.Report(job)),
                cancellationToken).ConfigureAwait(false);

            await writer.WriteLineAsync(
                $"{DateTime.UtcNow:O}\t{job.Status}\tInput={job.InputPath}\tOutput={job.OutputPath}\tError={job.ErrorMessage}");
            await writer.FlushAsync();
            progress?.Report(job);
        }

        await writer.WriteLineAsync("-");
        await writer.WriteLineAsync($"Batch finished (UTC): {DateTime.UtcNow:O}");
        await writer.FlushAsync();

        await _logger.LogAsync($"Batch completed. Log='{logPath}'").ConfigureAwait(false);
        return logPath;
    }
}
