using ImageAiEnhancerApp.Domain.Enums;

namespace ImageAiEnhancerApp.Domain.Models;

public class EnhanceOptions
{
    public EnhanceMode Mode { get; set; } = EnhanceMode.Basic;
    public int Scale { get; set; } = 2;
    public bool Sharpen { get; set; }
    public bool Denoise { get; set; }
    public int JpegQuality { get; set; } = 90;
    public string OutputFormat { get; set; } = "png";
    public string? SelectedModelName { get; set; }
    public bool UseGpuIfAvailable { get; set; } = true;

    public EnhanceOptions Clone()
    {
        return new EnhanceOptions
        {
            Mode = Mode,
            Scale = Scale,
            Sharpen = Sharpen,
            Denoise = Denoise,
            JpegQuality = JpegQuality,
            OutputFormat = OutputFormat,
            SelectedModelName = SelectedModelName,
            UseGpuIfAvailable = UseGpuIfAvailable
        };
    }
}