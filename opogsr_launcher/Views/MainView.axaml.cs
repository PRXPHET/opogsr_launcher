using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using System.Linq;

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
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;

            var focused = FocusManager.GetFocusedElement();

            if (e.Source is not null && focused is Control c && c.GetVisualAncestors().Contains(sender) && c.GetVisualDescendants().Contains(e.Source))
                return;

            // Start window dragging
            BeginMoveDrag(e);
        }
    }
}