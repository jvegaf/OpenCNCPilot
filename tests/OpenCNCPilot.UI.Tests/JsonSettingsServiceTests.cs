using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using OpenCNCPilot.UI.Services;
using Xunit;

public class JsonSettingsServiceTests
{
    [Fact]
    public async Task Load_Returns_Defaults_When_File_Missing()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "OpenCNCPilotTests", Path.GetRandomFileName());
        var svc = new JsonSettingsService(tmp);
        var s = await svc.LoadAsync();
        s.BaudRate.Should().Be(115200);
        s.DefaultComPort.Should().NotBeNull();
    }

    [Fact]
    public async Task Save_Then_Load_Roundtrips()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "OpenCNCPilotTests", Path.GetRandomFileName());
        var svc = new JsonSettingsService(tmp);
        var settings = new AppSettings { DefaultComPort = "ttyUSB0", BaudRate = 250000, LastGCodeDirectory = "/tmp" };
        await svc.SaveAsync(settings);
        var loaded = await svc.LoadAsync();
        loaded.DefaultComPort.Should().Be("ttyUSB0");
        loaded.BaudRate.Should().Be(250000);
        loaded.LastGCodeDirectory.Should().Be("/tmp");
    }
}
