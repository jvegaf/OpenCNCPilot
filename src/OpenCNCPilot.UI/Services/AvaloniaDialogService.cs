using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using OpenCNCPilot.UI.ViewModels;
using OpenCNCPilot.UI.Views;

namespace OpenCNCPilot.UI.Services;

public class AvaloniaDialogService : IDialogService
{
    private readonly Func<Window?> _getMainWindow;

    public AvaloniaDialogService(Func<Window?> getMainWindow)
    {
        _getMainWindow = getMainWindow;
    }

    public async Task<string[]?> OpenFilesAsync(string title, string? initialDirectory = null, string[]? filters = null, bool allowMultiple = false)
    {
        var top = _getMainWindow();
        if (top is null) return null;

        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = allowMultiple,
            SuggestedStartLocation = initialDirectory is not null ? await ToFolder(top, initialDirectory) : null,
            FileTypeFilter = filters is not null ? filters.Select(ToFilePickerType).ToList() : null
        };

        var result = await top.StorageProvider.OpenFilePickerAsync(options);
        return result?.Select(f => f.Path.LocalPath).ToArray();
    }

    public async Task<string?> SaveFileAsync(string title, string? initialDirectory = null, string? defaultFileName = null, string[]? filters = null)
    {
        var top = _getMainWindow();
        if (top is null) return null;

        var options = new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = defaultFileName,
            SuggestedStartLocation = initialDirectory is not null ? await ToFolder(top, initialDirectory) : null,
            FileTypeChoices = filters is not null ? filters.Select(ToFilePickerType).ToList() : null
        };

        var result = await top.StorageProvider.SaveFilePickerAsync(options);
        return result?.Path.LocalPath;
    }

    public async Task<string?> PickFolderAsync(string title, string? initialDirectory = null)
    {
        var top = _getMainWindow();
        if (top is null) return null;

        var options = new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = initialDirectory is not null ? await ToFolder(top, initialDirectory) : null
        };

        var result = await top.StorageProvider.OpenFolderPickerAsync(options);
        return result?.FirstOrDefault()?.Path.LocalPath;
    }

    public async Task AlertAsync(string title, string message, string okText = "OK")
    {
        var top = _getMainWindow();
        if (top is null) return;
        await ShowMessageWindowAsync(top, title, message, okText, null);
    }

    public async Task<bool> ConfirmAsync(string title, string message, string confirmText = "OK", string cancelText = "Cancel")
    {
        var top = _getMainWindow();
        if (top is null) return false;
        return await ShowMessageWindowAsync(top, title, message, confirmText, cancelText);
    }

    public async Task<double?> PromptNumberAsync(string title, string message, double? defaultValue = null, double? min = null, double? max = null, int decimals = 3)
    {
        var owner = _getMainWindow();
        if (owner is null) return null;

        double? result = null;
        var input = new TextBox { Width = 200 };
        if (defaultValue.HasValue)
            input.Text = defaultValue.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var btnOk = new Button { Content = "OK", MinWidth = 80 };
        var btnCancel = new Button { Content = "Cancel", MinWidth = 80 };

        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
            ColumnDefinitions = new ColumnDefinitions("Auto, *")
        };
        grid.Children.Add(new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
        Grid.SetRow(grid.Children[^1], 0);
        Grid.SetColumnSpan(grid.Children[^1], 2);

        grid.Children.Add(new TextBlock { Text = "Value:", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
        Grid.SetRow(grid.Children[^1], 1);
        Grid.SetColumn(grid.Children[^1], 0);

        grid.Children.Add(input);
        Grid.SetRow(grid.Children[^1], 1);
        Grid.SetColumn(grid.Children[^1], 1);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        buttons.Children.Add(btnCancel);
        buttons.Children.Add(btnOk);
        grid.Children.Add(buttons);
        Grid.SetRow(buttons, 2);
        Grid.SetColumnSpan(buttons, 2);

        var wnd = new Window
        {
            Title = title,
            Content = grid,
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            Padding = new Thickness(16),
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        btnOk.Click += (_, __) =>
        {
            if (double.TryParse(input.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var val))
            {
                if (min.HasValue && val < min.Value) val = min.Value;
                if (max.HasValue && val > max.Value) val = max.Value;
                result = Math.Round(val, Math.Clamp(decimals, 0, 10));
                wnd.Close();
            }
        };
        btnCancel.Click += (_, __) => { result = null; wnd.Close(); };

        await wnd.ShowDialog(owner);
        return result;
    }

    public async Task ShowWarningsAsync(string header, System.Collections.Generic.IEnumerable<string> warnings)
    {
        var owner = _getMainWindow();
        if (owner is null) return;

        var vm = new WarningWindowViewModel();
        vm.Load(header, warnings);
        var wnd = new WarningWindow { DataContext = vm };
        await wnd.ShowDialog(owner);
    }

    private static async Task<bool> ShowMessageWindowAsync(Window owner, string title, string message, string primaryText, string? closeText)
    {
        var result = false;
        var btnPrimary = new Button { Content = primaryText, MinWidth = 80 };
        var btnClose = closeText is not null ? new Button { Content = closeText, MinWidth = 80 } : null;

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        if (btnClose is not null)
        {
            buttons.Children.Add(btnClose);
        }
        buttons.Children.Add(btnPrimary);

        var content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                buttons
            }
        };

        var wnd = new Window
        {
            Title = title,
            Content = content,
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            Padding = new Thickness(16),
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        btnPrimary.Click += (_, __) => { result = true; wnd.Close(); };
        if (btnClose is not null)
            btnClose.Click += (_, __) => { result = false; wnd.Close(); };

        await wnd.ShowDialog(owner);
        return result;
    }

    private static async Task<IStorageFolder?> ToFolder(TopLevel top, string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                var folder = await top.StorageProvider.TryGetFolderFromPathAsync(path);
                return folder;
            }
        }
        catch { }
        return null;
    }

    private static FilePickerFileType ToFilePickerType(string pattern)
    {
        // pattern example: "GCode (*.gcode;*.nc)|*.gcode;*.nc"
        var parts = pattern.Split('|');
        var name = parts.Length > 0 ? parts[0] : "Files";
        var globs = parts.Length > 1 ? parts[1].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : Array.Empty<string>();
        return new FilePickerFileType(name)
        {
            Patterns = globs.Length > 0 ? globs : new[] { "*.*" }
        };
    }
}
