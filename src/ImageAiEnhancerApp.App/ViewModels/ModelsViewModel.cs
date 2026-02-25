using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageAiEnhancerApp.Core.Interfaces;
using ImageAiEnhancerApp.Core.Services;

namespace ImageAiEnhancerApp.App.ViewModels;

public sealed class ModelsViewModel : ObservableObject
{
    private readonly AppDataPaths _paths;
    private readonly IModelManager _modelManager;
    private readonly IOnnxUpscaleEnhancer _onnxEnhancer;
    private readonly IAppLogger _logger;

    private bool _isDirectMlAvailable;
    private bool _useGpuIfAvailable = true;
    private string _activeProvider = "CPU";

    public ModelsViewModel(
        AppDataPaths paths,
        IModelManager modelManager,
        IOnnxUpscaleEnhancer onnxEnhancer,
        IAppLogger logger)
    {
        _paths = paths;
        _modelManager = modelManager;
        _onnxEnhancer = onnxEnhancer;
        _logger = logger;

        Models = new ObservableCollection<ModelItemViewModel>();

        OpenModelsFolderCommand = new RelayCommand(OpenModelsFolder);
        ReloadModelsCommand = new RelayCommand(ReloadModels);

        ReloadModels();
    }

    public event Action? ModelsReloaded;

    public ObservableCollection<ModelItemViewModel> Models { get; }

    public RelayCommand OpenModelsFolderCommand { get; }
    public RelayCommand ReloadModelsCommand { get; }

    public bool IsDirectMlAvailable
    {
        get => _isDirectMlAvailable;
        private set => SetProperty(ref _isDirectMlAvailable, value);
    }

    public bool UseGpuIfAvailable
    {
        get => _useGpuIfAvailable;
        set
        {
            if (SetProperty(ref _useGpuIfAvailable, value))
            {
                ActiveProvider = _onnxEnhancer.GetActiveProviderName(_useGpuIfAvailable);
            }
        }
    }

    public string ActiveProvider
    {
        get => _activeProvider;
        private set => SetProperty(ref _activeProvider, value);
    }

    private void ReloadModels()
    {
        var models = _modelManager.ReloadModels();

        Models.Clear();
        foreach (var model in models)
        {
            var resolvedPath = _modelManager.ResolveModelPath(model);
            Models.Add(new ModelItemViewModel
            {
                Name = model.Name,
                Path = resolvedPath,
                Scale = model.Scale,
                Exists = File.Exists(resolvedPath),
                Notes = model.Notes ?? string.Empty
            });
        }

        IsDirectMlAvailable = _onnxEnhancer.IsDirectMlAvailable();
        ActiveProvider = _onnxEnhancer.GetActiveProviderName(UseGpuIfAvailable);

        _ = _logger.LogAsync($"ModelsView reload complete. Models={Models.Count}, DirectML={IsDirectMlAvailable}");
        ModelsReloaded?.Invoke();
    }

    private void OpenModelsFolder()
    {
        Directory.CreateDirectory(_paths.ModelsDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = _paths.ModelsDirectory,
            UseShellExecute = true
        });
    }
}
