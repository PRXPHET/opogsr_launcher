using System;
using Avalonia;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Threading;

namespace opogsr_launcher
{
    internal class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                if (!Debugger.IsAttached)
                {
                    Logger.Error("UNHANDLED EXCEPTION CAUGHT");
                    Logger.Error(ex.Message);

                    if (ex.StackTrace != null)
                    {
                        Logger.Error("Stack Trace:");
                        Logger.Error(ex.StackTrace);
                    }

                    throw;
                }
                else
                    throw;
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
