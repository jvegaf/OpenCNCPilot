using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenCNCPilot.Core.GCode;
using OpenCNCPilot.Hardware.Services;
using OpenCNCPilot.UI.Services;
using OpenCNCPilot.UI.ViewModels;
using OpenCNCPilot.UI.Views;

namespace OpenCNCPilot.UI;

public partial class App : Application
{
    private IServiceProvider? _serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Configure services
        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        _serviceProvider = serviceCollection.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Line below is needed to remove Avalonia data validation.
            // Without this line you will get duplicate validations from both Avalonia and CT
            BindingPlugins.DataValidators.RemoveAt(0);

            var mainViewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureServices(ServiceCollection services)
    {
        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Hardware services
        services.AddSingleton<ISerialPortService, SerialPortService>();
    services.AddSingleton<IGCodeSender, GCodeSender>();

        // Dialogs
        services.AddSingleton<IDialogService>(sp =>
            new AvaloniaDialogService(() =>
                (Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow as Window));

        // Settings
        services.AddSingleton<ISettingsService, JsonSettingsService>();

        // Core services: IGCodeParser is currently stateless between Parse() calls,
        // so Singleton is acceptable. Switch to Transient if internal state becomes per-parse.
        services.AddSingleton<IGCodeParser, GCodeParser>();
    services.AddSingleton<IGCodePathBuilder, GCodePathBuilder>();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<ViewModels.SettingsWindowViewModel>();
    services.AddTransient<ViewModels.GrblSettingsViewModel>();
    services.AddTransient<ViewModels.EditMacroItemViewModel>();
    services.AddTransient<ViewModels.FileViewModel>();
    }

    public static IServiceProvider? Services
        => Current is App app ? app._serviceProvider : null;
}
