using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using opogsr_common;
using opogsr_common.Log;
using opogsr_launcher.Extensions;
using opogsr_launcher.ViewModels;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace opogsr_launcher
{
    internal partial class App : Application
    {
        [DllImport("user32.dll")]
        private static extern uint GetDoubleClickTime();

        private DateTime? _trayLastClickTime;

        private readonly IServiceProvider _serviceProvider = ConfigureServiceProvider().BuildServiceProvider();

        public App()
        {
            DataContext = new AppViewModel();
        }

        private static ServiceCollection ConfigureServiceProvider()
        {
            ServiceCollection collection = new();

            collection.AddManagers();

            collection.AddViewModels();
            collection.AddViews();

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

        private void OnTrayIconClicked(object? sender, EventArgs e)
        {
            var now = DateTime.Now;

            if (_trayLastClickTime == null)
            {
                _trayLastClickTime = now;
                return;
            }

            var delta = (now - _trayLastClickTime.Value).TotalMilliseconds;

            if (delta <= GetDoubleClickTime())
            {
                _trayLastClickTime = null;
                ApplicationHelper.Show();
            }
            else
            {
                _trayLastClickTime = now;
            }
        }
    }
}