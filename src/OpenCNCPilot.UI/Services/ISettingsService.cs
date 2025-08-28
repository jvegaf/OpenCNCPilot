using System.Threading.Tasks;

namespace OpenCNCPilot.UI.Services;

public interface ISettingsService
{
    Task<AppSettings> LoadAsync();
    Task SaveAsync(AppSettings settings);
}

public class AppSettings
{
    public string DefaultComPort { get; set; } = string.Empty;
    public int BaudRate { get; set; } = 115200;
    public string LastGCodeDirectory { get; set; } = string.Empty;
    public bool IgnoreAdditionalAxes { get; set; } = true;
    public double ViewerRotateSensitivity { get; set; } = 0.3;
    public double ViewerPanSensitivity { get; set; } = 0.02;
    public double ViewerZoomStepFactor { get; set; } = 1.1;
}

