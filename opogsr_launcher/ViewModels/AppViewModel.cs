using ReactiveUI;
using ReactiveUI.SourceGenerators;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Text;

namespace opogsr_launcher.ViewModels
{
    public partial class AppViewModel : ViewModelBase
    {
        [Reactive] public partial ReactiveCommand<Unit, Unit> ShowWindowCommand { get; set; }
        [Reactive] public partial ReactiveCommand<Unit, Unit> ExitCommand { get; set; }

        public AppViewModel()
        {
            ShowWindowCommand = ReactiveCommand.Create(ApplicationHelper.Show);
            ExitCommand = ReactiveCommand.Create(ApplicationHelper.Close);
        }
    }
}
