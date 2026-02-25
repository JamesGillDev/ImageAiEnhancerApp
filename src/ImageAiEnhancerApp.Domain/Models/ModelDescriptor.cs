namespace ImageAiEnhancerApp.Domain.Models;

public class ModelDescriptor
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int Scale { get; set; }
    public string? InputName { get; set; }
    public string? OutputName { get; set; }
    public string? Notes { get; set; }
}