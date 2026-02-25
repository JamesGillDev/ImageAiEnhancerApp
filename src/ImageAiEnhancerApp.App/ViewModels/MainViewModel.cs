using CommunityToolkit.Mvvm.ComponentModel;
using ImageAiEnhancerApp.Core.Interfaces;
using ImageAiEnhancerApp.Core.Services;

namespace ImageAiEnhancerApp.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    public MainViewModel(
        AppDataPaths paths,
        IModelManager modelManager,
        IOnnxUpscaleEnhancer onnxEnhancer,
        IImageLoader imageLoader,
        IJobRunner jobRunner,
        IAppLogger logger)
    {
        SingleImage = new SingleImageViewModel(paths, modelManager, imageLoader, jobRunner);
        Batch = new BatchViewModel(paths, modelManager, imageLoader, jobRunner);
        Models = new ModelsViewModel(paths, modelManager, onnxEnhancer, logger);

        Models.ModelsReloaded += OnModelsReloaded;
    }

    public SingleImageViewModel SingleImage { get; }
    public BatchViewModel Batch { get; }
    public ModelsViewModel Models { get; }

    private void OnModelsReloaded()
    {
        SingleImage.ReloadModels();
        Batch.ReloadModels();
    }
}
