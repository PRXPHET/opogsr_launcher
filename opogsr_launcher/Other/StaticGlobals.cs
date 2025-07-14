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
            public const string Discord = "https://discord.gg/XsWasFeTrC";
            public const string ApPro = "https://ap-pro.ru/forums/topic/2371-op-ogsr";
            public const string AMK = "https://www.amk-team.ru/forum/forum/178-op-ogsr/";
        }

        public static class Locations
        {
            public static string Start { get; } = Directory.GetCurrentDirectory();

            public static string Bin { get; } = Start + "\\bin_x64";

            public static string Appdata { get; } = Start + "\\appdata";

            public static string Logs { get; } = Appdata + "\\logs";

            public static string Saves { get; } = Appdata + "\\savedgames";
        }

        public static class Extensions
        {
            public static string Save { get; } = ".cps";
        }

        public static class Names
        {
            public static string Executable { get; } = Init();

            public static string GameExecutable { get; } = "OPOGSR.exe";

            private static string Init()
            {
                string myName = System.Diagnostics.Process.GetCurrentProcess().ProcessName.ToLower();

                if (Path.GetExtension(myName) == string.Empty)
                    myName += ".exe";

                return myName;
            }
        }
    }
}
