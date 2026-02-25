namespace ImageAiEnhancerApp.Core.Interfaces;

public interface IAppLogger
{
    Task LogAsync(string message, CancellationToken cancellationToken = default);
}
