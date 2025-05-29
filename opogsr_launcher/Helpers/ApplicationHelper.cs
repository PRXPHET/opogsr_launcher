using Avalonia.Controls;

namespace opogsr_launcher
{
    internal class ApplicationHelper
    {
        public static Window MainWindow;

        public static void SetMainWindow(Window window)
        {
            MainWindow = window;
        }

        public static void Minimize()
        {
            MainWindow.WindowState = WindowState.Minimized;
        }

        public static void Maximize()
        {
            MainWindow.WindowState = MainWindow.WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
        }

        public static void Close()
        {
            MainWindow.Close();
        }
    }
}
