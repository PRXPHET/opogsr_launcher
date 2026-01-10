using ReactiveUI;
using System.Reactive;
using System.Reactive.Linq;

namespace opogsr_launcher.Other.Settings
{
    internal static class SettingsCommands
    {
        internal static readonly Token Langs =
        [
            "eng",
            "rus",
            "ukr"
        ];

        internal class SettingsControlLanguage : SettingsToken
        {
            public ReactiveCommand<string, Unit> LangSelectionCommand { get; }

            internal SettingsControlLanguage(string cmd, Token values, string defaultValue) : base(cmd, values, defaultValue, SettingType.Launcher)
            {
                LangSelectionCommand = ReactiveCommand.Create<string>(RuntimeResource.RuntimeResource.Instance.SetLanguage);

                this.WhenAnyValue(x => x.CurrentValue.Value)
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .InvokeCommand(LangSelectionCommand);
            }
        }
    }
}
