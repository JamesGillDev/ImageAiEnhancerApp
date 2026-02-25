using System.Windows;
using ImageAiEnhancerApp.App.ViewModels;
using ImageAiEnhancerApp.Core.Services;

namespace ImageAiEnhancerApp.App;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var paths = new AppDataPaths();
            paths.EnsureCreated();

            var logger = new AppLogger(paths);
            var modelManager = new ModelManager(paths, logger);
            modelManager.ReloadModels();

            var imageLoader = new ImageLoader();
            var basicEnhancer = new BasicEnhancer();
            var onnxEnhancer = new OnnxUpscaleEnhancer(logger);
            var jobRunner = new JobRunner(imageLoader, basicEnhancer, onnxEnhancer, modelManager, paths, logger);

            var mainViewModel = new MainViewModel(paths, modelManager, onnxEnhancer, imageLoader, jobRunner, logger);

            _ = logger.LogAsync("Application started.");

            var window = new MainWindow
            {
                DataContext = mainViewModel
            };

            window.Show();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                ex.Message,
                "Startup Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);

            Shutdown(-1);
        }
    }
}
