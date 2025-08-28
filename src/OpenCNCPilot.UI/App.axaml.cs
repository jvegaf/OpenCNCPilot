using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using OpenCNCPilot.UI.ViewModels;
using OpenCNCPilot.UI.Views;
using OpenCNCPilot.Hardware.Services;
using OpenCNCPilot.UI.Services;

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

        // Dialogs
        services.AddSingleton<IDialogService>(sp =>
            new AvaloniaDialogService(() =>
                (Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow as Window));

        // Settings
        services.AddSingleton<ISettingsService, JsonSettingsService>();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<ViewModels.SettingsWindowViewModel>();
    }

    public static IServiceProvider? Services => ((App)Current!)._serviceProvider;
}
