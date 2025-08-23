using System;
using Avalonia;
using Avalonia.ReactiveUI;
using System.Diagnostics;
using opogsr_launcher.Other;

namespace opogsr_launcher
{
    internal class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            GlobalExceptionHandler.Initialize();
            try
            {
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            catch(Exception ex)
            {
                GlobalExceptionHandler.LogException(ex);

                if (Debugger.IsAttached)
                    Debugger.Break();
                else
                    Environment.Exit(1);
            }
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
