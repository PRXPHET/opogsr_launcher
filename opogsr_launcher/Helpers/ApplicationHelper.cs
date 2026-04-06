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

        public static void Show()
        {
            MainWindow.Show();
            MainWindow.ShowInTaskbar = true;
        }

        public static void Hide()
        {
            MainWindow.Hide();
            MainWindow.ShowInTaskbar = false;
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

        public static void CloseOrMoveToTray()
        {
            bool tray = (bool)Other.Settings.Storage.GetValue("move_to_system_tray_on_close");
            if (tray)
                Hide();
            else
                Close();
        }
    }
}
