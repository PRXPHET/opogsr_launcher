using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace opogsr_launcher
{
    internal class Logger
    {
        public static string LogFileName { get { return Path.GetFileNameWithoutExtension(StaticGlobals.Names.Executable) + ".log"; } }

        private static string s_fileName;

        public static void SetOutputFile(string fileName)
        {
            s_fileName = fileName;

            if (File.Exists(s_fileName))
            {
                File.SetAttributes(s_fileName, FileAttributes.Normal);
                File.Delete(s_fileName);
            }
            else
            {
                string baseDir = Path.GetDirectoryName(s_fileName);
                if (!Directory.Exists(baseDir))
                {
                    Directory.CreateDirectory(baseDir);
                }
            }
        }

        private static Action<string> s_callback;

        public static void SetCallback(Action<string> callback)
        {
            s_callback = callback;
        }

        public static void Info(string message, params object[] args)
        {
            Write("~ " + message, args);
        }

        public static void Error(string message, params object[] args)
        {
            Write("! " + message, args);
        }

        public static void Ok(string message, params object[] args)
        {
            Write("# " + message, args);
        }

        private static void Write(string message, params object[] args)
        {
            WriteToFile(message, args);

            if (s_callback != null)
            {
                s_callback(string.Format(message, args));
            }
        }

        private static void WriteToFile(string message, params object[] args)
        {
            if (!string.IsNullOrEmpty(s_fileName))
            {
                try
                {
                    File.AppendAllText(s_fileName, DateTime.Now.ToShortTimeString() + " " + string.Format(message, args) + Environment.NewLine);
                }
                catch
                {
                    // спецом, чтобы не вылетать, если не может записать в файл
                }
            }
        }

        public static void Exception(Exception ex)
        {
            WriteToFile("! " + ex.Message);
            throw ex;
        }
    }
}
