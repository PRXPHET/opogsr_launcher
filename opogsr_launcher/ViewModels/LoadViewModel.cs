using opogsr_launcher.Helpers;
using opogsr_launcher.Other.RuntimeResource;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Text;

namespace opogsr_launcher.ViewModels
{
    public class SaveFile
    {
        public string FileName { get; set; }
        public string Text { get; set; }
        public string WriteTime { get; set; }
    }

    public class LoadViewModel : ViewModelBase
    {
        [Reactive] public ObservableCollection<SaveFile> Saves { get; set; } = new();

        [Reactive] public SaveFile SelectedSave { get; set; }

        [Reactive] public ReactiveCommand<Unit, Unit> LoadSaveCommand { get; set; }

        [Reactive] public RuntimeResourceString LoadSaveStr { get; set; }

        private Encoding GetEncoding()
        {
            try
            {
                return Encoding.GetEncoding("windows-1251");
            }
            catch
            {
                return Encoding.Default;
            }
        }

        private void LoadSavesList()
        {
            if (Directory.Exists(StaticGlobals.Locations.Saves))
            {
                DirectoryInfo di = new DirectoryInfo(StaticGlobals.Locations.Saves);
                FileInfo[] fi = di.GetFiles("*" + StaticGlobals.Extensions.Save);
                var sorted = fi.OrderByDescending(x => x.LastWriteTime);

                Encoding encoding = GetEncoding();
                Encoding EUTF8 = Encoding.UTF8;

                foreach (FileInfo t in sorted)
                {
                    string str = t.Name.Replace(StaticGlobals.Extensions.Save, "");
                    byte[] bytes = encoding.GetBytes(str);
                    string text = EUTF8.GetString(bytes);

                    Saves.Add(new SaveFile
                    {
                        FileName = t.Name,
                        Text = text,
                        WriteTime = t.LastWriteTime.ToString()
                    });
                }
            }
        }

        private void LoadSave()
        {
            LauncherHelper.LaunchGame(args: ["-start server(" + SelectedSave.FileName.Replace(StaticGlobals.Extensions.Save, "") + "/single/alife/load) client(localhost)"]);
            ApplicationHelper.MainWindow.Close();
        }

        public LoadViewModel()
        {
            LoadSaveStr = new("LoadSave");

            LoadSaveCommand = ReactiveCommand.Create(LoadSave);

            LoadSavesList();
        }
    }
}
