using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageAiEnhancerApp.App.Helpers;
using ImageAiEnhancerApp.Core.Interfaces;
using ImageAiEnhancerApp.Core.Services;
using ImageAiEnhancerApp.Domain.Enums;
using ImageAiEnhancerApp.Domain.Models;
using Microsoft.Win32;
using WinForms = System.Windows.Forms;

namespace ImageAiEnhancerApp.App.ViewModels;

public sealed class SingleImageViewModel : ObservableObject
{
    private readonly AppDataPaths _paths;
    private readonly IModelManager _modelManager;
    private readonly IImageLoader _imageLoader;
    private readonly IJobRunner _jobRunner;

    private CancellationTokenSource? _enhanceCts;

    private string _inputPath = string.Empty;
    private string _outputFolder = string.Empty;
    private string _outputSuffix = "_enhanced";
    private EnhanceMode _selectedMode = EnhanceMode.Basic;
    private int _selectedScale = 2;
    private bool _sharpen;
    private bool _denoise;
    private string _selectedOutputFormat = "png";
    private int _jpegQuality = 90;
    private bool _useGpuIfAvailable = true;
    private string? _selectedModelName;
    private string _statusMessage = "Drop an image here or browse to begin.";
    private string _modelStatusMessage = "Basic mode works without any ONNX model.";
    private double _progress;
    private double _comparePosition = 0.5;
    private bool _isBusy;
    private ImageSource? _beforeImage;
    private ImageSource? _afterImage;

    public SingleImageViewModel(
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

        BrowseInputImageCommand = new RelayCommand(BrowseInputImage);
        BrowseOutputFolderCommand = new RelayCommand(BrowseOutputFolder);
        EnhanceCommand = new AsyncRelayCommand(EnhanceAsync, CanEnhance);
        CancelCommand = new RelayCommand(CancelEnhance, CanCancel);
        LoadDroppedFileCommand = new RelayCommand<string>(LoadDroppedFile);

        ReloadModels();
    }

    public ObservableCollection<EnhanceMode> ModeOptions { get; }
    public ObservableCollection<int> ScaleOptions { get; }
    public ObservableCollection<string> OutputFormatOptions { get; }
    public ObservableCollection<ModelDescriptor> AvailableModels { get; }

    public RelayCommand BrowseInputImageCommand { get; }
    public RelayCommand BrowseOutputFolderCommand { get; }
    public AsyncRelayCommand EnhanceCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand<string> LoadDroppedFileCommand { get; }

    public string InputPath
    {
        get => _inputPath;
        set
        {
            if (SetProperty(ref _inputPath, value))
            {
                LoadBeforePreview();
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

    public EnhanceMode SelectedMode
    {
        get => _selectedMode;
        set
        {
            if (SetProperty(ref _selectedMode, value))
            {
                RefreshModelStatus();
            }
        }
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
        set
        {
            if (SetProperty(ref _selectedModelName, value))
            {
                RefreshModelStatus();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string ModelStatusMessage
    {
        get => _modelStatusMessage;
        set => SetProperty(ref _modelStatusMessage, value);
    }

    public double Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, Math.Clamp(value, 0d, 1d));
    }

    public double ComparePosition
    {
        get => _comparePosition;
        set => SetProperty(ref _comparePosition, Math.Clamp(value, 0d, 1d));
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommandStates();
            }
        }
    }

    public ImageSource? BeforeImage
    {
        get => _beforeImage;
        private set
        {
            if (SetProperty(ref _beforeImage, value))
            {
                OnPropertyChanged(nameof(HasBeforeImage));
            }
        }
    }

    public ImageSource? AfterImage
    {
        get => _afterImage;
        private set
        {
            if (SetProperty(ref _afterImage, value))
            {
                OnPropertyChanged(nameof(HasAfterImage));
            }
        }
    }

    public bool HasBeforeImage => BeforeImage is not null;
    public bool HasAfterImage => AfterImage is not null;

    public void ReloadModels()
    {
        var models = _modelManager.ReloadModels();

        AvailableModels.Clear();
        foreach (var model in models)
        {
            AvailableModels.Add(model);
        }

        if (AvailableModels.Count == 0)
        {
            SelectedModelName = null;
            RefreshModelStatus();
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedModelName) ||
            AvailableModels.All(m => !string.Equals(m.Name, SelectedModelName, StringComparison.OrdinalIgnoreCase)))
        {
            SelectedModelName = AvailableModels[0].Name;
        }

        RefreshModelStatus();
    }

    private void BrowseInputImage()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Images|*.jpg;*.jpeg;*.png;*.webp;*.tif;*.tiff",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            InputPath = dialog.FileName;
            StatusMessage = "Image loaded.";
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

    private async Task EnhanceAsync()
    {
        if (!File.Exists(InputPath))
        {
            StatusMessage = "Input image not found.";
            return;
        }

        if (!_imageLoader.IsSupportedImagePath(InputPath))
        {
            StatusMessage = "Unsupported format. Use JPG, PNG, WEBP, or TIFF.";
            return;
        }

        if (SelectedMode == EnhanceMode.AiUpscale)
        {
            var model = _modelManager.FindByName(SelectedModelName);
            if (model is null)
            {
                StatusMessage = "Model not found. Add it to Models/models.json and reload models.";
                return;
            }

            var resolvedPath = _modelManager.ResolveModelPath(model);
            if (!File.Exists(resolvedPath))
            {
                StatusMessage = $"Model not found at '{resolvedPath}'. Place super-resolution-10.onnx in ./Models.";
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(OutputFolder))
        {
            OutputFolder = _paths.OutputDirectory;
        }

        var outputPath = _jobRunner.BuildOutputPath(InputPath, OutputFolder, OutputSuffix, SelectedOutputFormat);

        var job = new EnhanceJob
        {
            InputPath = InputPath,
            OutputPath = outputPath,
            Options = BuildEnhanceOptions(),
            Status = JobStatus.Pending
        };

        _enhanceCts = new CancellationTokenSource();
        IsBusy = true;
        Progress = 0d;
        StatusMessage = "Enhancing image...";

        try
        {
            var progress = new Progress<double>(value =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => Progress = value);
            });

            await _jobRunner.RunJobAsync(job, progress, _enhanceCts.Token);

            if (job.Status == JobStatus.Done)
            {
                AfterImage = ImageSourceFactory.FromFile(job.OutputPath);
                ComparePosition = 0.5d;
                StatusMessage = $"Done. Saved: {job.OutputPath}";
            }
            else if (job.Status == JobStatus.Canceled)
            {
                StatusMessage = "Enhancement canceled.";
            }
            else
            {
                StatusMessage = string.IsNullOrWhiteSpace(job.ErrorMessage)
                    ? "Enhancement failed."
                    : $"Enhancement failed: {job.ErrorMessage}";
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Enhancement canceled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Enhancement failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            _enhanceCts?.Dispose();
            _enhanceCts = null;
        }
    }

    private bool CanEnhance()
    {
        return !IsBusy && !string.IsNullOrWhiteSpace(InputPath);
    }

    private void CancelEnhance()
    {
        _enhanceCts?.Cancel();
    }

    private bool CanCancel()
    {
        return IsBusy;
    }

    private void LoadDroppedFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        InputPath = path;
        StatusMessage = "Image loaded from drag & drop.";
    }

    private EnhanceOptions BuildEnhanceOptions()
    {
        return new EnhanceOptions
        {
            Mode = SelectedMode,
            Scale = SelectedScale,
            Sharpen = Sharpen,
            Denoise = Denoise,
            OutputFormat = SelectedOutputFormat,
            JpegQuality = JpegQuality,
            SelectedModelName = SelectedModelName,
            UseGpuIfAvailable = UseGpuIfAvailable
        };
    }

    private void LoadBeforePreview()
    {
        BeforeImage = ImageSourceFactory.FromFile(InputPath);
        AfterImage = null;
        ComparePosition = 0.5d;
    }

    private void RefreshModelStatus()
    {
        if (SelectedMode == EnhanceMode.Basic)
        {
            ModelStatusMessage = "Basic mode works without any ONNX model.";
            return;
        }

        var model = _modelManager.FindByName(SelectedModelName);
        if (model is null)
        {
            ModelStatusMessage = "No AI model selected. Use Models tab to configure models.json.";
            return;
        }

        var path = _modelManager.ResolveModelPath(model);
        ModelStatusMessage = File.Exists(path)
            ? $"AI model ready: {model.Name} ({path})"
            : $"Model missing at {path}. Place the file and click Reload Models.";
    }

    private void RefreshCommandStates()
    {
        EnhanceCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }
}
