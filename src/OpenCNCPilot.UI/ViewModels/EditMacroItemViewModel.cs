using System;
using System.Reactive;
using ReactiveUI;

namespace OpenCNCPilot.UI.ViewModels;

public class EditMacroItemViewModel : ReactiveObject
{
    private string macroName = string.Empty;
    private string commands = string.Empty;
    private bool useMacros;

    public string MacroName
    {
        get => macroName;
        set => this.RaiseAndSetIfChanged(ref macroName, value);
    }

    public string Commands
    {
        get => commands;
        set => this.RaiseAndSetIfChanged(ref commands, value);
    }

    public bool UseMacros
    {
        get => useMacros;
        set => this.RaiseAndSetIfChanged(ref useMacros, value);
    }

    public ReactiveCommand<Unit, EditMacroResult?> OkCommand { get; }
    public ReactiveCommand<Unit, EditMacroResult?> CancelCommand { get; }

    public Interaction<EditMacroResult?, EditMacroResult?> Close { get; } = new();

    public EditMacroItemViewModel()
    {
        var canOk = this.WhenAnyValue(x => x.MacroName, x => x.Commands,
            (name, cmds) => IsValid(name) && IsValid(cmds) && !string.IsNullOrWhiteSpace(name));

        OkCommand = ReactiveCommand.Create(() =>
        {
            if (!IsValid(MacroName) || !IsValid(Commands))
                return null;
            var result = new EditMacroResult(MacroName.Trim(), Commands, UseMacros);
            _ = Close.Handle(result).Subscribe();
            return result;
        }, canOk);

        CancelCommand = ReactiveCommand.Create(() =>
        {
            _ = Close.Handle(null).Subscribe();
            return (EditMacroResult?)null;
        });
    }

    private static bool IsValid(string s)
        => s is not null && !s.Contains(':') && !s.Contains(';');
}

public record EditMacroResult(string Name, string Commands, bool UseMacros);
