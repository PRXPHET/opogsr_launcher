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
                    .ObserveOn(RxSchedulers.MainThreadScheduler)
                    .InvokeCommand(LangSelectionCommand);
            }
        }

        internal static readonly Token SmapToken = 
        [
            "1536x1536",
            "2048x2048",
            "2560x2560",
            "3072x3072",
            "4096x4096",
            "6144x6144",
            "8192x8192"
        ];

        internal static readonly Token AAToken =
        [
            "st_opt_off",
            "st_opt_smaa",
            "st_opt_taa",
            "st_opt_fsr2",
            "st_opt_dlss"
        ];

        internal static readonly Token AOToken =
        [
            "st_opt_off",
            "st_ssdo",
            "st_gtao"
        ];

        internal static readonly Token AOQualityToken =
        [
            "st_opt_low",
            "st_opt_medium",
            "st_opt_high"
        ];

        internal static readonly Token SunQualityToken =
        [
            "st_opt_low",
            "st_opt_medium",
            "st_opt_high",
            "st_opt_ultra",
            "st_opt_extreme"
        ];

        internal static readonly Token SunshaftsModeToken =
        [
            "st_opt_off",
            "ss_ogse",
            "ss_manowar",
            "volumetric",
            "combined_ogse",
            "combined_manowar"
        ];

        internal static readonly Token GameDifficultyToken =
        [
            "gd_novice",
            "gd_stalker",
            "gd_veteran",
            "gd_master"
        ];
    }
}
