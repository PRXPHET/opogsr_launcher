using Avalonia.Controls;

namespace opogsr_launcher
{
    public partial class MainView : Window
    {
        public MainView()
        {
            ApplicationHelper.SetMainWindow(this);

            InitializeComponent();
        }

        private void OnPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                // Начинаем перемещение окна
                this.BeginMoveDrag(e);
            }
        }
    }
}