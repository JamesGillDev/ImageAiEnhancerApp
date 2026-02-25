using System.Text.Json;
using ImageAiEnhancerApp.Core.Interfaces;
using ImageAiEnhancerApp.Domain.Models;

namespace ImageAiEnhancerApp.Core.Services;

public sealed class ModelManager : IModelManager
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly AppDataPaths _paths;
    private readonly IAppLogger _logger;
    private List<ModelDescriptor> _models = new();

    public ModelManager(AppDataPaths paths, IAppLogger logger)
    {
        _paths = paths;
        _logger = logger;
        _paths.EnsureCreated();
    }

    public IReadOnlyList<ModelDescriptor> Models => _models;

    public IReadOnlyList<ModelDescriptor> ReloadModels()
    {
        EnsureDefaultModelConfigExists();

        try
        {
            var json = File.ReadAllText(_paths.ModelsConfigPath);
            var parsed = JsonSerializer.Deserialize<List<ModelDescriptor>>(json, SerializerOptions) ?? new List<ModelDescriptor>();

            _models = parsed
                .Where(m => !string.IsNullOrWhiteSpace(m.Name) && !string.IsNullOrWhiteSpace(m.Path))
                .Select(m => new ModelDescriptor
                {
                    Name = m.Name.Trim(),
                    Path = m.Path.Trim(),
                    Scale = m.Scale <= 0 ? 1 : m.Scale,
                    InputName = string.IsNullOrWhiteSpace(m.InputName) ? null : m.InputName.Trim(),
                    OutputName = string.IsNullOrWhiteSpace(m.OutputName) ? null : m.OutputName.Trim(),
                    Notes = string.IsNullOrWhiteSpace(m.Notes) ? null : m.Notes.Trim()
                })
                .ToList();

            _ = _logger.LogAsync($"ModelManager loaded {_models.Count} model entries from '{_paths.ModelsConfigPath}'.");
        }
        catch (Exception ex)
        {
            _models = new List<ModelDescriptor>();
            _ = _logger.LogAsync($"ModelManager failed to load models: {ex.Message}");
        }

        return _models;
    }

    public ModelDescriptor? FindByName(string? modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return null;
        }

        return _models.FirstOrDefault(m => string.Equals(m.Name, modelName, StringComparison.OrdinalIgnoreCase));
    }

    public string ResolveModelPath(ModelDescriptor descriptor)
    {
        if (Path.IsPathRooted(descriptor.Path))
        {
            return descriptor.Path;
        }

        var normalized = descriptor.Path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var modelsPrefix = $"Models{Path.DirectorySeparatorChar}";

        if (normalized.StartsWith(modelsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFullPath(Path.Combine(_paths.RootPath, normalized));
        }

        return Path.GetFullPath(Path.Combine(_paths.ModelsDirectory, normalized));
    }

    private void EnsureDefaultModelConfigExists()
    {
        if (File.Exists(_paths.ModelsConfigPath))
        {
            return;
        }

        var defaultModels = new List<ModelDescriptor>
        {
            new()
            {
                Name = "SuperResolution10",
                Path = "Models/super-resolution-10.onnx",
                Scale = 3,
                Notes = "YCbCr luma-only; expects Y input; output is Y upscaled."
            }
        };

        Directory.CreateDirectory(Path.GetDirectoryName(_paths.ModelsConfigPath) ?? _paths.ModelsDirectory);
        var json = JsonSerializer.Serialize(defaultModels, SerializerOptions);
        File.WriteAllText(_paths.ModelsConfigPath, json);
    }
}
