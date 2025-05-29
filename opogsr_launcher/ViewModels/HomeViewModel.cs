using opogsr_launcher.Managers;
using opogsr_launcher.Other.RuntimeResource;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;

namespace opogsr_launcher.ViewModels
{
    public class HomeViewModel : ViewModelBase
    {
        private static DiscordRPCManager _discordRPCManager;
        private static GithubManager _githubManager;

        public RuntimeResource Resource { get; private set; }

        [Reactive] public bool IsDownloading { get; set; }

        [Reactive] public string DownloadSpeedStr { get; set; }

        [Reactive] public RuntimeResourceString ValidationStr { get; set; }

        [Reactive] public RuntimeResourceString ButtonMainStr { get; set; }

        [Reactive] public string SizeStr { get; set; }

        [Reactive] public double Progress { get; set; }

        [Reactive] public ReactiveCommand<Unit, Unit> ButtonMainCommand { get; set; }

        public HomeViewModel(DiscordRPCManager discordRPCManager, GithubManager githubManager, RuntimeResource resource)
        {
            _discordRPCManager = discordRPCManager;
            _githubManager = githubManager;
            Resource = resource;

            ValidationStr = new(resource, "");
            ButtonMainStr = new(resource, "StartDownload");

            ButtonMainCommand = ReactiveCommand.Create(ButtonMain);
        }

        private async void UpdateProgressBar()
        {
            // https://stackoverflow.com/a/4975942
            static String BytesToString(ulong bytes)
            {
                string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
                int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
                double num = Math.Round(bytes / Math.Pow(1024, place), 1);
                return num.ToString() + suf[place];
            }

            while (_githubManager.state.totalSize == 0 && _githubManager.state.bytesRead.IsEmpty)
                await Task.Delay(1000);

            ulong prev_size = 0;
            ulong cur_size = 0;
            ulong total_size = _githubManager.state.totalSize;

            while(IsDownloading)
            {
                await Task.Delay(1000);

                cur_size = _githubManager.state.bytesRead.Values.Aggregate((a, b) => a + b);

                DownloadSpeedStr = $"{BytesToString(cur_size - prev_size)}/s";

                prev_size = cur_size;

                SizeStr = $"{BytesToString(cur_size)} / {BytesToString(total_size)}";

                Progress = Math.Floor(cur_size * (1.0 / total_size) * 100);

                _discordRPCManager.Client.UpdateState(string.Format(Properties.Resources.StateDownloading, Progress));
            }

            _discordRPCManager.Client.UpdateState(Properties.Resources.StateIdling);
        }

        public async void ValidateFiles()
        {
            ValidationStr.Key = "ValidatingFiles";

            FileStates state = await _githubManager.Validate();

            if (state == FileStates.NoFiles)
                ButtonMainStr.Key = "StartDownload";
            else if (state == FileStates.NeedUpdate)
                ButtonMainStr.Key = "StartUpdate";
            else
            {
                ButtonMainStr.Key = "Play";
                StaticGlobals.Stats.CanPlay = true;
            }

            ValidationStr.Key = "";
        }

        private async void StartDownload()
        {
            IsDownloading = true;

            ValidationStr.Key = "Downloading";

            UpdateProgressBar();

            StaticGlobals.Stats.CanPlay = await _githubManager.DownloadInvalid();

            if (StaticGlobals.Stats.CanPlay)
            {
                ButtonMainStr.Key = "Play";
            }

            IsDownloading = false;

            ValidationStr.Key = "";
        }

        private void StartGame()
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
                    FileName = path,
                    UseShellExecute = true,
                    CreateNoWindow = true
                }
            };

            process.Start();

            ApplicationHelper.MainWindow.Close();
        }

        private void ButtonMain()
        {
            if (StaticGlobals.Stats.CanPlay)
                StartGame();
            else if (!IsDownloading)
                StartDownload();
        }
    }
}
