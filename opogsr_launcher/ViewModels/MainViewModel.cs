using opogsr_common.Log;
using opogsr_launcher.Managers;
using opogsr_launcher.Other.RuntimeResource;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using opogsr_common;
using opogsr_launcher.Properties;
using MsBox.Avalonia;
using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Media;
using System.Linq;
using System.Reactive;
using System.Reflection;
using System.Threading.Tasks;
using opogsr_launcher.Managers;
using opogsr_launcher.Other.RuntimeResource;

namespace opogsr_launcher.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        public class Lang
        {
            public string Key { get; set; }
            public string Title { get; set; }
        }

        private static DiscordRPCManager _discordRPCManager;

        private static HomeViewModel _HomePage;
        private static LoadViewModel _LoadPage;
        private static SettingsViewModel _SettingsPage;
        private static LogViewModel _LogPage;

        public ReactiveCommand<Unit, Unit> GoToHomeCommand { get; }
        public ReactiveCommand<Unit, Unit> GoToLoadCommand { get; }
        public ReactiveCommand<Unit, Unit> GoToSettingsCommand { get; }
        public ReactiveCommand<Unit, Unit> GoToLogCommand { get; }

        public ReactiveCommand<string, Unit> OpenLinkCommand { get; }

        public ReactiveCommand<Unit, Unit> MinimizeCommand { get; }
        public ReactiveCommand<Unit, Unit> MaximizeCommand { get; }
        public ReactiveCommand<Unit, Unit> CloseCommand { get; }

        [Reactive] public partial ViewModelBase CurrentPage { get; set; }

        [Reactive] public partial bool HomeActive { get; private set; }
        [Reactive] public partial bool LoadActive { get; private set; }
        [Reactive] public partial bool SettingsActive { get; private set; }
        [Reactive] public partial bool LogActive { get; private set; }

        private static void OpenLink(string link)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = link,
                UseShellExecute = true
            });
        }

        public MainViewModel(HomeViewModel homeViewModel, LoadViewModel loadViewModel, SettingsViewModel settingsViewModel, LogViewModel logViewModel, DiscordRPCManager discordRPCManager)
        {
            _discordRPCManager = discordRPCManager;

            _HomePage = homeViewModel;
            _LoadPage = loadViewModel;
            _SettingsPage = settingsViewModel;
            _LogPage = logViewModel;

            string InformationalVersion = Assembly.GetEntryAssembly()!.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

            Logger.Info("OP OGSR Launcher. Build Date: " + InformationalVersion + ".");
            Logger.Info("Start...");

            _HomePage.ValidateFiles();

            this.WhenAnyValue(x => x.CurrentPage).Subscribe(page =>
            {
                HomeActive = page == _HomePage;
                LoadActive = page == _LoadPage;
                SettingsActive = page == _SettingsPage;
                LogActive = page == _LogPage;
            });

            OpenLinkCommand = ReactiveCommand.Create<string>(OpenLink);

            string lang = (string)Other.Settings.Storage.GetValue("launcher_language");
            if (lang != RuntimeResource.Instance.Language)
                RuntimeResource.Instance.SetLanguage(lang);

            CurrentPage = _HomePage;

            GoToHomeCommand = ReactiveCommand.Create(() => { CurrentPage = _HomePage; } );
            GoToLoadCommand = ReactiveCommand.Create(() => { CurrentPage = _LoadPage; });
            GoToSettingsCommand = ReactiveCommand.Create(() => { CurrentPage = _SettingsPage; });
            GoToLogCommand = ReactiveCommand.Create(() => { CurrentPage = _LogPage; });

            MinimizeCommand = ReactiveCommand.Create(ApplicationHelper.Minimize);
            MaximizeCommand = ReactiveCommand.Create(ApplicationHelper.Maximize);
            CloseCommand = ReactiveCommand.Create(ApplicationHelper.Close);
        }
    }
}
