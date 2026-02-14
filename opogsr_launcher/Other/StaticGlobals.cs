using System.Diagnostics;
using System.IO;

namespace opogsr_launcher
{
    public static class StaticGlobals
    {
        public static class Stats
        {
            public static bool CanPlay { get; set; } = false;
        }

        public static class Variables
        {
            public const long LargeChunkSize = 2 * 1000 * 1000 * 1000;
            //public const long LargeChunkSize = 2 * 1000 * 1000;
        }

        public static class Links
        {
            public const string Youtube = "https://www.youtube.com/@prxphet666";
            public const string Steam = "https://store.steampowered.com/franchise/stalker";
            public const string Discord = "https://discord.gg/XsWasFeTrC";

            public const string Yoomoney = "https://yoomoney.ru/to/4100117348307270";
            public const string Boosty = "https://boosty.to/prxphet";
            public const string DonationAlerts = "https://dalink.to/prxphet";

            public const string GithubRateLimit = "https://api.github.com/rate_limit";
        }

        public static class Locations
        {
            public static string Start { get; } = Directory.GetCurrentDirectory();

            public static string Bin { get; } = Start + "\\bin_x64";

            public static string Appdata { get; } = Start + "\\appdata";

            public static string Launcher { get; } = Appdata + "\\launcher";

            public static string Saves { get; } = Appdata + "\\savedgames";

            public static string Settings { get; } = Appdata + "\\settings";

            public static string Logs { get; } = Launcher + "\\logs";
        }

        public static class Extensions
        {
            public static string Save { get; } = ".cps";
        }

        public static class Names
        {
            public const string GameScriptSettingsFile = "opogsr_options.ltx";
            public const string GameSettingsFile = "user.ltx";
            public const string LauncherSettingsFile = "settings.ltx";

            public const string GameExecutable = "OPOGSR.exe";

            public static string Executable { get; } = Path.GetFileName(System.Environment.ProcessPath);
        }
    }
}
