using System.Collections.Generic;
using System.Linq;
using ReactiveUI;

namespace OpenCNCPilot.UI.ViewModels;

public class WarningWindowViewModel : ReactiveObject
{
    private string text = string.Empty;

    public string Text
    {
        get => text;
        set => this.RaiseAndSetIfChanged(ref text, value);
    }

    public void Load(string header, IEnumerable<string> warnings)
    {
        var lines = warnings.Select((w, i) => $"{i + 1} > {w}");
        Text = header + string.Join('\n', lines);
    }
}
