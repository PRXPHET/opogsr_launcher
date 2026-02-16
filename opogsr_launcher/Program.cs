using Avalonia;
using ReactiveUI;
using ReactiveUI.Avalonia;
using System;
using System.Reactive;
using opogsr_common.Exceptions;

namespace opogsr_launcher
{
    internal class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            GlobalExceptionHandler.Initialize();
            RxApp.DefaultExceptionHandler = Observer.Create<Exception>(GlobalExceptionHandler.LogException);
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
