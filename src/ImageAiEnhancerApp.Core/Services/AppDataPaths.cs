namespace ImageAiEnhancerApp.Core.Services;

public sealed class AppDataPaths
{
    public AppDataPaths(string? rootPath = null)
    {
        RootPath = string.IsNullOrWhiteSpace(rootPath) ? Directory.GetCurrentDirectory() : rootPath;
        LogsDirectory = Path.Combine(RootPath, "App_Data", "Logs");
        ModelsDirectory = Path.Combine(RootPath, "Models");
        OutputDirectory = Path.Combine(RootPath, "Output");
        ModelsConfigPath = Path.Combine(ModelsDirectory, "models.json");
        AppLogPath = Path.Combine(LogsDirectory, "app.log");
    }

    public string RootPath { get; }
    public string LogsDirectory { get; }
    public string ModelsDirectory { get; }
    public string OutputDirectory { get; }
    public string ModelsConfigPath { get; }
    public string AppLogPath { get; }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(ModelsDirectory);
        Directory.CreateDirectory(OutputDirectory);
    }
}
