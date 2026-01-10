using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace opogsr_launcher.Other.Settings
{
    internal static class SettingsLtxManager
    {
        private static readonly Dictionary<SettingType, string> Paths = new()
        {
            { SettingType.User, Path.Combine(StaticGlobals.Locations.Appdata, StaticGlobals.Names.GameSettingsFile) },
            { SettingType.Script, Path.Combine(StaticGlobals.Locations.Settings, StaticGlobals.Names.GameScriptSettingsFile) },
            { SettingType.Launcher, Path.Combine(StaticGlobals.Locations.Launcher, StaticGlobals.Names.LauncherSettingsFile) },
        };

        public static void Write(SettingType type, Dictionary<string, string> values)
        {
            string path = Paths[type];

            string directory = Path.GetDirectoryName(path);

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            if (!File.Exists(path))
                File.WriteAllText(path, string.Empty, Encoding.UTF8);

            var sep = type != SettingType.User ? "= " : " ";
            var lines = File.ReadAllLines(path);

            for (int i = 0; i < lines.Length; i++)
            {
                var raw = lines[i];
                var trimmed = raw.TrimStart();
                if (trimmed.Length == 0) continue;

                int delim = trimmed.IndexOfAny([' ', '=']);
                if (delim < 0) continue;
                var key = trimmed[..delim].ToString();

                if (!values.TryGetValue(key, out var newVal)) continue;

                var keyField = type != SettingType.User ? key.PadRight(33) : key;
                lines[i] = $"{keyField}{sep}{newVal}";

                values.Remove(key);
            }

            if (values.Count != 0)
            {
                var extra = values.Select(kv =>
                {
                    string kf = type != SettingType.User ? kv.Key.PadRight(33) : kv.Key;
                    return $"{kf}{sep}{kv.Value}";
                });
                File.AppendAllLines(path, extra, Encoding.UTF8);
            }
            else
            {
                File.WriteAllLines(path, lines, Encoding.UTF8);
            }
        }

        public static Dictionary<string, string>? Read(SettingType type)
        {
            string path = Paths[type];

            if (!File.Exists(path))
                return null;

            var values = new Dictionary<string, string>();

            foreach (var rawLine in File.ReadLines(path))
            {
                var line = rawLine.Trim();
                if (line.Length == 0) continue;

                // Will add support for it later
                if (line.StartsWith("bind ", StringComparison.OrdinalIgnoreCase)) continue;

                int eq = line.IndexOf('=');
                if (eq < 0) // "key value"
                {
                    var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2) continue;
                    values[parts[0]] = parts[1];
                }
                else // "key = value"
                {
                    var key = line[..eq].TrimEnd();
                    var value = line[(eq + 1)..].TrimStart();
                    if (key.Length == 0) continue;
                    values[key] = value;
                }
            }

            return values;
        }
    }
}
