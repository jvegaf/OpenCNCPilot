using System;
using System.Linq;
using System.IO;
using Avalonia;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Controls;
using OpenCNCPilot.UI.ViewModels;

namespace OpenCNCPilot.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += MainWindow_DataContextChanged;

        // Configurar drag-and-drop programáticamente para el viewport
        if (this.FindControl<Control>("Viewport") is { } viewport)
        {
            DragDrop.SetAllowDrop(viewport, true);
            viewport.AddHandler(DragDrop.DragOverEvent, Viewport_DragOver, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            viewport.AddHandler(DragDrop.DropEvent, Viewport_Drop, Avalonia.Interactivity.RoutingStrategies.Bubble);
        }
    }

    private void MainWindow_DataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.ShowSettings.RegisterHandler(async interaction =>
            {
                var dlg = new SettingsWindow
                {
                    DataContext = interaction.Input,
                };
                var result = await dlg.ShowDialog<bool?>(this);
                interaction.SetOutput(result);
            });

            vm.ShowGrblSettings.RegisterHandler(async interaction =>
            {
                var dlg = new GrblSettingsWindow
                {
                    DataContext = interaction.Input,
                };
                var result = await dlg.ShowDialog<bool?>(this);
                interaction.SetOutput(result);
            });
        }
    }

    // Drag-and-drop de archivos sobre el viewport
    private void Viewport_DragOver(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            var files = e.Data.GetFiles();
            if (files != null && files.Any())
            {
                e.DragEffects = DragDropEffects.Copy;
                e.Handled = true;
                return;
            }
        }
        e.DragEffects = DragDropEffects.None;
        e.Handled = true;
    }

    private async void Viewport_Drop(object? sender, DragEventArgs e)
    {
        try
        {
            if (DataContext is not MainWindowViewModel vm) return;
            if (!e.Data.Contains(DataFormats.Files)) return;
            var items = e.Data.GetFiles();
            var first = items?.FirstOrDefault();
            if (first == null) return;
            // Intentar obtener path local
            var path = first.TryGetLocalPath();
            if (string.IsNullOrEmpty(path))
            {
                // Si no es un archivo local, intentar abrir stream y crear temporal (fallback)
                if (first is IStorageFile sfile)
                {
                    await using var stream = await sfile.OpenReadAsync();
                    var temp = Path.GetTempFileName();
                    await using (var fs = File.OpenWrite(temp))
                    {
                        await stream.CopyToAsync(fs);
                    }
                    path = temp;
                }
                else
                {
                    return; // No es un archivo manejable
                }
            }

            // Cargar a través del FileViewModel y luego ajustar vista
            await vm.FileTab.LoadFromPathAsync(path);
            vm.FitRequestId++;
        }
        catch
        {
            // swallow; opcionalmente logear si agregamos ILogger aquí
        }
    }
}
