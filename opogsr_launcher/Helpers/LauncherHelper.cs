using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using opogsr_common;
using opogsr_common.Log;

namespace opogsr_launcher.Helpers
{
    internal static class LauncherHelper
    {
        public static void LaunchGame(IList<string>? args = null)
        {
            string path = Path.Combine(StaticGlobals.Locations.Bin, StaticGlobals.Names.GameExecutable);

            if (!File.Exists(path))
            {
                Logger.Exception(new Exception("Game executable not found. Something strange."));
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    Arguments = args is not null ? string.Join(" ", args) : string.Empty,
                    FileName = path,
                    UseShellExecute = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
        }
    }
}
