using CommunityToolkit.Mvvm.ComponentModel;
using ImageAiEnhancerApp.Domain.Models;

namespace ImageAiEnhancerApp.App.ViewModels;

public sealed class BatchJobItemViewModel : ObservableObject
{
    public BatchJobItemViewModel(EnhanceJob job)
    {
        Job = job;
    }

    public EnhanceJob Job { get; }

    public string InputPath => Job.InputPath;
    public string OutputPath => Job.OutputPath;
    public string Status => Job.Status.ToString();
    public double Progress => Job.Progress;
    public double ProgressPercent => Job.Progress * 100d;
    public string ErrorMessage => Job.ErrorMessage ?? string.Empty;

    public void Refresh()
    {
        OnPropertyChanged(nameof(OutputPath));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(Progress));
        OnPropertyChanged(nameof(ProgressPercent));
        OnPropertyChanged(nameof(ErrorMessage));
    }
}
