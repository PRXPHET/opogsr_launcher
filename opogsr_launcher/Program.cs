using System;
using Avalonia;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Microsoft.Extensions.Configuration;

namespace opogsr_launcher
{
    internal class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .ConfigureFonts(fontManager => { fontManager.AddFontCollection(new AppFontCollection()); })
                .LogToTrace()
                .UseReactiveUI();
                
    }
}
