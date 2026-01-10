global using SettingsControls = System.Collections.Generic.List<opogsr_launcher.Other.Settings.ISettingsControl>;

using System.Collections.Generic;

namespace opogsr_launcher.Other.Settings
{
    internal static class SettingsPages
    {
        private static readonly SettingsControls LauncherSettings = new()
        {
            { new SettingsCommands.SettingsControlLanguage("launcher_language", SettingsCommands.Langs, SettingsCommands.Langs.Contains(RuntimeResource.RuntimeResource.Instance.Language) ? RuntimeResource.RuntimeResource.Instance.Language : SettingsCommands.Langs[0]) },
            { new SettingsSeparator("sep_launcher_warn") },
            { new SettingsRange<int>("max_download_threads", 1, 16, 1, 8, SettingType.Launcher) },
            { new SettingsRange<int>("max_amount_of_tries", 1, 20, 1, 5, SettingType.Launcher) }
        };
        public static readonly Dictionary<string, SettingsControls> Pages = new()
        {
            { "tab_launcher", LauncherSettings },
        };
    }
}
