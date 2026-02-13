using Avalonia.Controls;
using opogsr_launcher.Helpers;
using opogsr_launcher.Managers;
using opogsr_launcher.Managers.Github;
using opogsr_launcher.Other.Converters;
using opogsr_launcher.Other.RuntimeResource;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Media;
using System.Reactive;
using System.Threading.Tasks;

namespace opogsr_launcher.ViewModels
{
    public partial class HomeViewModel : ViewModelBase
    {
        private static DiscordRPCManager _discordRPCManager;
        private static GithubDownloadManager _githubDownloadManager;

        [Reactive] public partial bool IsDownloading { get; set; }

        [Reactive] public partial string DownloadSpeedStr { get; set; }

        [Reactive] public partial RuntimeResourceString StateStr { get; set; }

        [Reactive] public partial RuntimeResourceString ButtonMainStr { get; set; }

        [Reactive] public partial string SizeStr { get; set; }

        [Reactive] public partial double Progress { get; set; }

        [Reactive] public partial bool ButtonMainVisible { get; set; }

        [Reactive] public partial ReactiveCommand<Unit, Unit> ButtonMainCommand { get; set; }

        public HomeViewModel(DiscordRPCManager discordRPCManager)
        {
            _githubDownloadManager = new(Other.Secrets.Secrets.GithubToken, Other.Secrets.Secrets.GithubRepo, Other.Secrets.Secrets.IsGithubPublicRepo);

            _discordRPCManager = discordRPCManager;

            ButtonMainVisible = false;

            StateStr = new("Connecting");
            ButtonMainStr = new("StartDownload");

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

            ulong total_size = _githubDownloadManager.Size();

            while (IsDownloading)
            {
                await Task.Delay(1000);

                if (FileProgress.IsEmpty)
                    continue;

                if (_githubDownloadManager.IsRateLimited)
                {
                    if (StateStr.Key != "RateLimited")
                        StateStr.Key = "RateLimited";

                    continue;
                }
                else if (!_githubDownloadManager.IsRateLimited && !string.IsNullOrEmpty(StateStr.Key) && StateStr.Key != "Downloading")
                {
                    StateStr.Key = "Downloading";
                }

                cur_size = FileProgress.Values.Aggregate((a, b) => a + b);

                DownloadSpeedStr = $"{BytesToString.Convert(cur_size - prev_size)}/s";

                prev_size = cur_size;

                SizeStr = $"{BytesToString.Convert(cur_size)} / {BytesToString.Convert(total_size)}";

                Progress = Math.Floor(cur_size * (1.0 / total_size) * 100);

                _discordRPCManager.Client.UpdateState(string.Format(Properties.Resources.StateDownloading, Progress));
            }

            _discordRPCManager.Client.UpdateState(Properties.Resources.StateIdling);
        }

        public async Task ValidateFiles()
        {
            if (Design.IsDesignMode)
                return;

            ButtonMainVisible = false;

            StateStr.Key = "Connecting";

            if (!await _githubDownloadManager.TryInitialize())
            {
                StateStr.Key = "ConnectionFailed";
                ButtonMainStr.Key = "TryConnectAgain";
                ButtonMainVisible = true;
                return;
            }

            StateStr.Key = "ValidatingFiles";

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

            StateStr.Key = "";

            ButtonMainVisible = true;
        }

        private async void StartDownload()
        {
            IsDownloading = true;
            ButtonMainVisible = false;

            StateStr.Key = "Downloading";

            var progress = new Progress<(string Name, ulong Total)>();

            UpdateProgressBar(progress);

            StaticGlobals.Stats.CanPlay = await _githubDownloadManager.DownloadInvalid(progress);

            if (StaticGlobals.Stats.CanPlay)
            {
                SystemSounds.Beep.Play();
                ButtonMainStr.Key = "Play";
            }

            IsDownloading = false;
            ButtonMainVisible = true;

            StateStr.Key = "";
        }

        private static void StartGame()
        {
            LauncherHelper.LaunchGame();
            ApplicationHelper.MainWindow.Close();
        }

        private void ButtonMain()
        {
            if (ButtonMainStr.Key == "TryConnectAgain")
                Task.Run(ValidateFiles);
            else if (StaticGlobals.Stats.CanPlay)
                StartGame();
            else if (!IsDownloading)
                StartDownload();
        }
    }
}
