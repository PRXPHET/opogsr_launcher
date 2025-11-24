using System;

namespace opogsr_launcher.Other.Converters
{
    public class BytesToString
    {
        private const string bad = "0B";
        
        private static readonly string[] suf = ["B", "KB", "MB", "GB", "TB", "PB", "EB"];

        // https://stackoverflow.com/a/4975942
        public static string Convert(ulong bytes)
        {
            if (bytes == 0)
                return bad;

            int place = System.Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return num.ToString() + suf[place];
        }
    }
}
