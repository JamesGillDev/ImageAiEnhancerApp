using ImageAiEnhancerApp.Domain.Models;

namespace ImageAiEnhancerApp.Core.Interfaces;

public interface IModelManager
{
    IReadOnlyList<ModelDescriptor> Models { get; }
    IReadOnlyList<ModelDescriptor> ReloadModels();
    ModelDescriptor? FindByName(string? modelName);
    string ResolveModelPath(ModelDescriptor descriptor);
}
