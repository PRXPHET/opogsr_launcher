using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using opogsr_launcher.Managers;
using opogsr_launcher.Other.Converters;
using opogsr_launcher.Other.RuntimeResource;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Concurrent;
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
        private static GithubDownloadManager _githubDownloadManager;

        public RuntimeResource Resource { get; private set; }

        [Reactive] public bool IsDownloading { get; set; }

        [Reactive] public string DownloadSpeedStr { get; set; }

        [Reactive] public RuntimeResourceString ValidationStr { get; set; }

        [Reactive] public RuntimeResourceString ButtonMainStr { get; set; }

        [Reactive] public string SizeStr { get; set; }

        [Reactive] public double Progress { get; set; }

        [Reactive] public ReactiveCommand<Unit, Unit> ButtonMainCommand { get; set; }

        public HomeViewModel(IServiceProvider serviceProvider, DiscordRPCManager discordRPCManager, RuntimeResource resource)
        {
            IConfiguration config = serviceProvider.GetRequiredService<IConfiguration>();

            string token = config["GithubToken"];
            string repo = config["GithubRepo"];

            _githubDownloadManager = new(token, repo);

            _discordRPCManager = discordRPCManager;
            Resource = resource;

            ValidationStr = new(resource, "");
            ButtonMainStr = new(resource, "StartDownload");

            ButtonMainCommand = ReactiveCommand.Create(ButtonMain);
        }

        private async void UpdateProgressBar(Progress<(string Name, ulong Total)> progress)
        {
            ConcurrentDictionary<string, ulong> FileProgress = new();

            ulong prev_size = 0;
            ulong cur_size = 0;

            progress.ProgressChanged += (_, data) =>
            {
                FileProgress[data.Name] = data.Total;
            };

            ulong total_size = await _githubDownloadManager.Size();

            while (IsDownloading)
            {
                await Task.Delay(1000);

                if (FileProgress.IsEmpty)
                    continue;

                cur_size = FileProgress.Values.Aggregate((a, b) => a + b);

                DownloadSpeedStr = $"{BytesToString.Convert(cur_size - prev_size)}/s";

                prev_size = cur_size;

                SizeStr = $"{BytesToString.Convert(cur_size)} / {BytesToString.Convert(total_size)}";

                Progress = Math.Floor(cur_size * (1.0 / total_size) * 100);

                _discordRPCManager.Client.UpdateState(string.Format(Properties.Resources.StateDownloading, Progress));
            }

            _discordRPCManager.Client.UpdateState(Properties.Resources.StateIdling);
        }

        public async void ValidateFiles()
        {
            ValidationStr.Key = "ValidatingFiles";

            FileStates state = await _githubDownloadManager.Validate();

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

            var progress = new Progress<(string Name, ulong Total)>();

            UpdateProgressBar(progress);

            StaticGlobals.Stats.CanPlay = await _githubDownloadManager.DownloadInvalid(progress);

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
