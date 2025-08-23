using ReactiveUI;
using System;
using System.Diagnostics;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;

namespace opogsr_launcher.Other
{
    public static class GlobalExceptionHandler
    {
        public static void Initialize()
        {
            RxApp.DefaultExceptionHandler = Observer.Create<Exception>(LogException);

            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                LogException(e.Exception);

                if (Debugger.IsAttached)
                    Debugger.Break();

                e.SetObserved();
            };

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    LogException(ex);
                    if (Debugger.IsAttached)
                        Debugger.Break();
                    else
                        Environment.Exit(1);
                }
            };
        }

        public static void LogException(Exception ex)
        {
            ArgumentNullException.ThrowIfNull(ex);

            Logger.Error($"UNHANDLED EXCEPTION CAUGHT: {ex.GetType().FullName} - {ex.Message}");
            Logger.Error($"Stack Trace: {ex.StackTrace ?? "not available."}");

            if (ex.InnerException != null)
            {
                Logger.Error($"Inner Exception: {ex.InnerException.GetType().FullName} - {ex.InnerException.Message}");
                Logger.Error($"Inner Stack Trace: {ex.InnerException.StackTrace ?? "not available."}");
            }
        }
    }
}
