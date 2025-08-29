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
    public bool PauseFileOnHold { get; set; } = false;
    public bool IgnoreAdditionalAxes { get; set; } = true;
    public double ViewerRotateSensitivity { get; set; } = 0.3;
    public double ViewerPanSensitivity { get; set; } = 0.02;
    public double ViewerZoomStepFactor { get; set; } = 1.1;
    public bool ViewerShowGrid { get; set; } = true;
    public bool ViewerShowOrigin { get; set; } = true;
    public bool ViewerShowBounds { get; set; } = false;
    public double ViewerGridMinPixelStep { get; set; } = 30.0;
    public double ViewerFlattenTolerance { get; set; } = 0.05;
    // Viewer styling
    public uint ViewerRapidColor { get; set; } = 0xFF19B4FF; // ARGB
    public uint ViewerCutColor { get; set; } = 0xFFFF7828;   // ARGB
    public double ViewerRapidStrokeWidth { get; set; } = 1.2;
    public double ViewerCutStrokeWidth { get; set; } = 1.6;
}

