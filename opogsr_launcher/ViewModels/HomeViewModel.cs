using Avalonia.Controls;
using MsBox.Avalonia;
using opogsr_common;
using opogsr_common.Managers;
using opogsr_common.Extensions;
using opogsr_launcher.Helpers;
using opogsr_launcher.Managers;
using opogsr_launcher.Other.RuntimeResource;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Media;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace opogsr_launcher.ViewModels
{
    public partial class HomeViewModel : ViewModelBase
    {
        private static DiscordRPCManager _discordRPCManager;
        private static LauncherGithubManager _launcherGithubManager;

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
            _launcherGithubManager = new(Secrets.GithubToken, Secrets.GithubRepo, Secrets.IsGithubPublicRepo);

            _discordRPCManager = discordRPCManager;

            ButtonMainVisible = false;

            StateStr = new("Connecting");
            ButtonMainStr = new("StartDownload");

            ButtonMainCommand = ReactiveCommand.Create(ButtonMain);
        }

        private async void UpdateProgressBar(Progress<(string Name, ulong Total)> progress, ulong total_size)
        {
            ConcurrentDictionary<string, ulong> FileProgress = new();

            ulong prev_size = 0;
            ulong cur_size = 0;

            progress.ProgressChanged += (_, data) =>
            {
                FileProgress[data.Name] = data.Total;
            };

            while (IsDownloading)
            {
                await Task.Delay(1000);

                if (FileProgress.IsEmpty)
                    continue;

                if (_launcherGithubManager.IsRateLimited)
                {
                    if (StateStr.Key != "RateLimited")
                        StateStr.Key = "RateLimited";

                    continue;
                }
                else if (!_launcherGithubManager.IsRateLimited && !string.IsNullOrEmpty(StateStr.Key) && StateStr.Key != "Downloading")
                {
                    StateStr.Key = "Downloading";
                }

                cur_size = FileProgress.Values.Aggregate((a, b) => a + b);

                DownloadSpeedStr = $"{Convert.BytesToString(cur_size - prev_size)}/s";

                prev_size = cur_size;

                SizeStr = $"{Convert.BytesToString(cur_size)} / {Convert.BytesToString(total_size)}";

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

            if (!await _launcherGithubManager.TryInitialize())
            {
                StateStr.Key = "ConnectionFailed";
                ButtonMainStr.Key = "TryConnectAgain";
                ButtonMainVisible = true;
                return;
            }

            if (!await _launcherGithubManager.ValidateLauncher())
            {
                bool updated = await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    var box = MessageBoxManager.GetMessageBoxCustom(new MsBox.Avalonia.Dto.MessageBoxCustomParams()
                    {
                        ContentTitle = RuntimeResource.GetString("LauncherUpdateTitle"),
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        CanResize = false,
                        ShowInCenter = true,
                        Topmost = true,

                        ContentMessage = RuntimeResource.GetString("LauncherUpdateDesc"),
                        ButtonDefinitions =
                        [
                            new() {Name = RuntimeResource.GetString("MsgBoxYes"), IsDefault = true},
                            new() {Name = RuntimeResource.GetString("MsgBoxNo"), IsCancel = true}
                        ],
                    });

                    string result = await box.ShowAsync();
                    if (result == RuntimeResource.GetString("MsgBoxYes"))
                    {
                        await StartLauncherUpdate();
                        return true;
                    }

                    return false;
                });

                if (updated)
                    return;
            }

            StateStr.Key = "ValidatingFiles";

            FileStates state = await _launcherGithubManager.Validate();

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

        private async Task StartLauncherUpdate()
        {
            IsDownloading = true;
            ButtonMainVisible = false;

            StateStr.Key = "Downloading";

            var progress = new Progress<(string Name, ulong Total)>();

            UpdateProgressBar(progress, _launcherGithubManager.Size(StaticGlobals.Names.Executable));

            await _launcherGithubManager.UpdateLauncher(progress);

            StateStr.Key = "";

            IsDownloading = false;
        }

        private async void StartDownload()
        {
            IsDownloading = true;
            ButtonMainVisible = false;

            StateStr.Key = "Downloading";

            var progress = new Progress<(string Name, ulong Total)>();

            UpdateProgressBar(progress, _launcherGithubManager.Size());

            StaticGlobals.Stats.CanPlay = await _launcherGithubManager.DownloadInvalid(progress);

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
