using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageAiEnhancerApp.Core.Interfaces;
using ImageAiEnhancerApp.Core.Services;
using ImageAiEnhancerApp.Domain.Enums;
using ImageAiEnhancerApp.Domain.Models;
using WinForms = System.Windows.Forms;

namespace ImageAiEnhancerApp.App.ViewModels;

public sealed class BatchViewModel : ObservableObject
{
    private static readonly HashSet<string> DefaultExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    private readonly AppDataPaths _paths;
    private readonly IModelManager _modelManager;
    private readonly IImageLoader _imageLoader;
    private readonly IJobRunner _jobRunner;

    private CancellationTokenSource? _batchCts;

    private string _inputFolder = string.Empty;
    private string _outputFolder = string.Empty;
    private string _outputSuffix = "_enhanced";
    private bool _includeSubfolders;
    private bool _includeTiff = true;
    private EnhanceMode _selectedMode = EnhanceMode.Basic;
    private int _selectedScale = 2;
    private bool _sharpen;
    private bool _denoise;
    private string _selectedOutputFormat = "png";
    private int _jpegQuality = 90;
    private bool _useGpuIfAvailable = true;
    private string? _selectedModelName;
    private bool _isRunning;
    private double _overallProgress;
    private string _batchStatusMessage = "Select folders and build the queue.";
    private string _lastLogPath = string.Empty;

    public BatchViewModel(
        AppDataPaths paths,
        IModelManager modelManager,
        IImageLoader imageLoader,
        IJobRunner jobRunner)
    {
        _paths = paths;
        _modelManager = modelManager;
        _imageLoader = imageLoader;
        _jobRunner = jobRunner;

        _paths.EnsureCreated();
        _outputFolder = _paths.OutputDirectory;

        ModeOptions = new ObservableCollection<EnhanceMode>
        {
            EnhanceMode.Basic,
            EnhanceMode.AiUpscale
        };

        ScaleOptions = new ObservableCollection<int> { 2, 3, 4 };
        OutputFormatOptions = new ObservableCollection<string> { "png", "jpg" };
        AvailableModels = new ObservableCollection<ModelDescriptor>();
        QueueItems = new ObservableCollection<BatchJobItemViewModel>();

        BrowseInputFolderCommand = new RelayCommand(BrowseInputFolder);
        BrowseOutputFolderCommand = new RelayCommand(BrowseOutputFolder);
        BuildQueueCommand = new RelayCommand(BuildQueue, CanBuildQueue);
        StartBatchCommand = new AsyncRelayCommand(StartBatchAsync, CanStartBatch);
        CancelBatchCommand = new RelayCommand(CancelBatch, CanCancelBatch);

        QueueItems.CollectionChanged += (_, _) => RefreshCommandStates();

        ReloadModels();
    }

    public ObservableCollection<EnhanceMode> ModeOptions { get; }
    public ObservableCollection<int> ScaleOptions { get; }
    public ObservableCollection<string> OutputFormatOptions { get; }
    public ObservableCollection<ModelDescriptor> AvailableModels { get; }
    public ObservableCollection<BatchJobItemViewModel> QueueItems { get; }

    public RelayCommand BrowseInputFolderCommand { get; }
    public RelayCommand BrowseOutputFolderCommand { get; }
    public RelayCommand BuildQueueCommand { get; }
    public AsyncRelayCommand StartBatchCommand { get; }
    public RelayCommand CancelBatchCommand { get; }

    public string InputFolder
    {
        get => _inputFolder;
        set
        {
            if (SetProperty(ref _inputFolder, value))
            {
                RefreshCommandStates();
            }
        }
    }

    public string OutputFolder
    {
        get => _outputFolder;
        set => SetProperty(ref _outputFolder, value);
    }

    public string OutputSuffix
    {
        get => _outputSuffix;
        set => SetProperty(ref _outputSuffix, value);
    }

    public bool IncludeSubfolders
    {
        get => _includeSubfolders;
        set => SetProperty(ref _includeSubfolders, value);
    }

    public bool IncludeTiff
    {
        get => _includeTiff;
        set => SetProperty(ref _includeTiff, value);
    }

    public EnhanceMode SelectedMode
    {
        get => _selectedMode;
        set => SetProperty(ref _selectedMode, value);
    }

    public int SelectedScale
    {
        get => _selectedScale;
        set => SetProperty(ref _selectedScale, value);
    }

    public bool Sharpen
    {
        get => _sharpen;
        set => SetProperty(ref _sharpen, value);
    }

    public bool Denoise
    {
        get => _denoise;
        set => SetProperty(ref _denoise, value);
    }

    public string SelectedOutputFormat
    {
        get => _selectedOutputFormat;
        set => SetProperty(ref _selectedOutputFormat, value);
    }

    public int JpegQuality
    {
        get => _jpegQuality;
        set => SetProperty(ref _jpegQuality, Math.Clamp(value, 1, 100));
    }

    public bool UseGpuIfAvailable
    {
        get => _useGpuIfAvailable;
        set => SetProperty(ref _useGpuIfAvailable, value);
    }

    public string? SelectedModelName
    {
        get => _selectedModelName;
        set => SetProperty(ref _selectedModelName, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                RefreshCommandStates();
            }
        }
    }

    public double OverallProgress
    {
        get => _overallProgress;
        private set => SetProperty(ref _overallProgress, Math.Clamp(value, 0d, 1d));
    }

    public string BatchStatusMessage
    {
        get => _batchStatusMessage;
        private set => SetProperty(ref _batchStatusMessage, value);
    }

    public string LastLogPath
    {
        get => _lastLogPath;
        private set => SetProperty(ref _lastLogPath, value);
    }

    public void ReloadModels()
    {
        var models = _modelManager.ReloadModels();

        AvailableModels.Clear();
        foreach (var model in models)
        {
            AvailableModels.Add(model);
        }

        if (AvailableModels.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(SelectedModelName) ||
                AvailableModels.All(m => !string.Equals(m.Name, SelectedModelName, StringComparison.OrdinalIgnoreCase)))
            {
                SelectedModelName = AvailableModels[0].Name;
            }
        }
        else
        {
            SelectedModelName = null;
        }
    }

    private void BrowseInputFolder()
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description = "Select input folder",
            UseDescriptionForTitle = true,
            InitialDirectory = string.IsNullOrWhiteSpace(InputFolder) ? _paths.RootPath : InputFolder
        };

        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            InputFolder = dialog.SelectedPath;
        }
    }

    private void BrowseOutputFolder()
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description = "Select output folder",
            UseDescriptionForTitle = true,
            InitialDirectory = string.IsNullOrWhiteSpace(OutputFolder) ? _paths.OutputDirectory : OutputFolder
        };

        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            OutputFolder = dialog.SelectedPath;
        }
    }

    private bool CanBuildQueue()
    {
        return !IsRunning && Directory.Exists(InputFolder);
    }

    private void BuildQueue()
    {
        if (!Directory.Exists(InputFolder))
        {
            BatchStatusMessage = "Input folder not found.";
            return;
        }

        if (string.IsNullOrWhiteSpace(OutputFolder))
        {
            OutputFolder = _paths.OutputDirectory;
        }

        Directory.CreateDirectory(OutputFolder);

        var extensions = new HashSet<string>(DefaultExtensions, StringComparer.OrdinalIgnoreCase);
        if (IncludeTiff)
        {
            extensions.Add(".tif");
            extensions.Add(".tiff");
        }

        var searchOption = IncludeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory
            .EnumerateFiles(InputFolder, "*.*", searchOption)
            .Where(path => extensions.Contains(Path.GetExtension(path)))
            .Where(path => _imageLoader.IsSupportedImagePath(path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        QueueItems.Clear();
        foreach (var file in files)
        {
            var outputPath = _jobRunner.BuildOutputPath(file, OutputFolder, OutputSuffix, SelectedOutputFormat);
            var job = new EnhanceJob
            {
                InputPath = file,
                OutputPath = outputPath,
                Options = BuildEnhanceOptions(),
                Status = JobStatus.Pending,
                Progress = 0
            };

            QueueItems.Add(new BatchJobItemViewModel(job));
        }

        OverallProgress = 0d;
        BatchStatusMessage = QueueItems.Count == 0
            ? "No supported images were found."
            : $"Queued {QueueItems.Count} image(s).";

        RefreshCommandStates();
    }

    private bool CanStartBatch()
    {
        return !IsRunning && QueueItems.Count > 0;
    }

    private async Task StartBatchAsync()
    {
        if (QueueItems.Count == 0)
        {
            return;
        }

        if (SelectedMode == EnhanceMode.AiUpscale)
        {
            var model = _modelManager.FindByName(SelectedModelName);
            if (model is null)
            {
                BatchStatusMessage = "No AI model selected. Configure Models/models.json first.";
                return;
            }

            var resolvedModelPath = _modelManager.ResolveModelPath(model);
            if (!File.Exists(resolvedModelPath))
            {
                BatchStatusMessage = $"Model not found at '{resolvedModelPath}'.";
                return;
            }
        }

        _batchCts = new CancellationTokenSource();
        IsRunning = true;
        OverallProgress = 0d;
        BatchStatusMessage = "Batch run started...";

        var jobs = QueueItems.Select(item => item.Job).ToList();
        foreach (var jobItem in QueueItems)
        {
            jobItem.Job.Options = BuildEnhanceOptions();
            jobItem.Job.Status = JobStatus.Pending;
            jobItem.Job.Progress = 0d;
            jobItem.Job.ErrorMessage = null;
            jobItem.Refresh();
        }

        var byId = QueueItems.ToDictionary(item => item.Job.Id, item => item);

        try
        {
            var progress = new Progress<EnhanceJob>(job =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (byId.TryGetValue(job.Id, out var viewModel))
                    {
                        viewModel.Refresh();
                    }

                    UpdateOverallProgress();
                });
            });

            LastLogPath = await _jobRunner.RunBatchAsync(jobs, progress, _batchCts.Token);

            var done = jobs.Count(j => j.Status == JobStatus.Done);
            var failed = jobs.Count(j => j.Status == JobStatus.Failed);
            var canceled = jobs.Count(j => j.Status == JobStatus.Canceled);

            BatchStatusMessage = $"Batch finished. Done={done}, Failed={failed}, Canceled={canceled}.";
        }
        catch (OperationCanceledException)
        {
            BatchStatusMessage = "Batch canceled.";
        }
        catch (Exception ex)
        {
            BatchStatusMessage = $"Batch failed: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
            _batchCts?.Dispose();
            _batchCts = null;
            UpdateOverallProgress();
        }
    }

    private void CancelBatch()
    {
        _batchCts?.Cancel();
        BatchStatusMessage = "Canceling batch...";
    }

    private bool CanCancelBatch()
    {
        return IsRunning;
    }

    private EnhanceOptions BuildEnhanceOptions()
    {
        return new EnhanceOptions
        {
            Mode = SelectedMode,
            Scale = SelectedScale,
            Sharpen = Sharpen,
            Denoise = Denoise,
            JpegQuality = JpegQuality,
            OutputFormat = SelectedOutputFormat,
            SelectedModelName = SelectedModelName,
            UseGpuIfAvailable = UseGpuIfAvailable
        };
    }

    private void UpdateOverallProgress()
    {
        if (QueueItems.Count == 0)
        {
            OverallProgress = 0d;
            return;
        }

        OverallProgress = QueueItems.Average(item => item.Progress);
    }

    private void RefreshCommandStates()
    {
        BuildQueueCommand.NotifyCanExecuteChanged();
        StartBatchCommand.NotifyCanExecuteChanged();
        CancelBatchCommand.NotifyCanExecuteChanged();
    }
}
