namespace ImageAiEnhancerApp.App.ViewModels;

public sealed class ModelItemViewModel
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public required int Scale { get; init; }
    public required bool Exists { get; init; }
    public string Notes { get; init; } = string.Empty;
}
