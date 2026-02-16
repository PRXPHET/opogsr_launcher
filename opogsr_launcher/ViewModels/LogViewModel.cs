using opogsr_common.Log;
using Avalonia.Media;
using System.Collections.Generic;
using ReactiveUI.SourceGenerators;
using System.Collections.ObjectModel;
using System.Threading;

namespace opogsr_launcher.ViewModels
{
    public class LogText
    {
        public string Text { get; set; }
        public SolidColorBrush Brush { get; set; }
    };

    public partial class LogViewModel : ViewModelBase
    {
        private static SolidColorBrush WhiteBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255));

        private Dictionary<string, SolidColorBrush> Colors = new()
        {
            ["! "] = new SolidColorBrush(Color.FromRgb(255, 0, 0)),
            ["- "] = new SolidColorBrush(Color.FromRgb(255, 255, 0)),
            ["# "] = new SolidColorBrush(Color.FromRgb(0, 255, 0)),
            ["~ "] = WhiteBrush,
        };

        [Reactive] public partial ObservableCollection<LogText> Logs { get; set; }

        private readonly Lock LogLock = new();

        private SolidColorBrush GetBrush(string s)
        {
            SolidColorBrush r = WhiteBrush;

            if (!string.IsNullOrWhiteSpace(s))
            {
                foreach (var c in Colors)
                {
                    if (s.StartsWith(c.Key))
                    {
                        r = c.Value;
                        break;
                    }
                }
            }

            return r;
        }

        public LogViewModel() 
        {
            Logs = [];

            Logger.SetCallback(LogCallback);
        }

        private void LogCallback(string msg)
        {
            SolidColorBrush brush = GetBrush(msg);
            lock (LogLock)
            {
                Logs.Add(new LogText()
                {
                    Text = msg,
                    Brush = brush
                });
            }
        }
    }
}
