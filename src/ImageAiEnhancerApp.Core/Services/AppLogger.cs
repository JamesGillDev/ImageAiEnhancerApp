using System.Text;
using ImageAiEnhancerApp.Core.Interfaces;

namespace ImageAiEnhancerApp.Core.Services;

public sealed class AppLogger : IAppLogger
{
    private readonly AppDataPaths _paths;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public AppLogger(AppDataPaths paths)
    {
        _paths = paths;
    }

    public async Task LogAsync(string message, CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();
        var line = $"{DateTime.UtcNow:O} {message}";

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await File.AppendAllTextAsync(_paths.AppLogPath, line + Environment.NewLine, Encoding.UTF8, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }
}
