using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using opogsr_launcher.Extensions;
using opogsr_launcher.Other.RuntimeResource;
using opogsr_launcher.ViewModels;
using System;
using System.IO;
using System.Text;

namespace opogsr_launcher
{
    public partial class App : Application
    {
        private readonly IServiceProvider _serviceProvider = ConfigureServiceProvider().BuildServiceProvider();

        private static ServiceCollection ConfigureServiceProvider()
        {
            ServiceCollection collection = new();

            collection.AddManagers();

            collection.AddViewModels();
            collection.AddViews();

            collection.AddSingleton<RuntimeResource>();

            return collection;
        }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            Logger.SetOutputFile(Path.Combine(StaticGlobals.Locations.Logs, Logger.LogFileName));

            MainViewModel mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainView { DataContext = mainViewModel };
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
            {
                singleViewPlatform.MainView = new MainView { DataContext = mainViewModel };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}