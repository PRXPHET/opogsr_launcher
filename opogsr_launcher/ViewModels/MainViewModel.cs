using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Reactive;
using opogsr_launcher.Properties;
using MsBox.Avalonia;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Avalonia.Controls;
using opogsr_launcher.Managers;
using opogsr_launcher.Other.RuntimeResource;

namespace opogsr_launcher.ViewModels
{
    public class MainViewModel : ViewModelBase
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

        public RuntimeResource Resource { get; private set; }

        public ReactiveCommand<Unit, Unit> GoToHomeCommand { get; }
        public ReactiveCommand<Unit, Unit> GoToLoadCommand { get; }
        public ReactiveCommand<Unit, Unit> GoToSettingsCommand { get; }
        public ReactiveCommand<Unit, Unit> GoToLogCommand { get; }

        public ReactiveCommand<string, Unit> OpenLinkCommand { get; }
        public ReactiveCommand<string, Unit> LangSelectionCommand { get; }

        public ReactiveCommand<Unit, Unit> MinimizeCommand { get; }
        public ReactiveCommand<Unit, Unit> MaximizeCommand { get; }
        public ReactiveCommand<Unit, Unit> CloseCommand { get; }

        [Reactive] public ViewModelBase CurrentPage { get; set; }

        [Reactive] public bool HomeActive { get; private set; }
        [Reactive] public bool LoadActive { get; private set; }
        [Reactive] public bool SettingsActive { get; private set; }
        [Reactive] public bool LogActive { get; private set; }

        [Reactive] public ObservableCollection<Lang> Langs { get; private set; } =
        [
            new() { Key = "eng", Title = "English" },
            new() { Key = "rus", Title = "Русский" },
            new() { Key = "ukr", Title = "Українська" }
        ];

        [Reactive] public Lang selectedLang { get; set; }

        private static void OpenLink(string link)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = link,
                UseShellExecute = true
            });
        }

        public MainViewModel(HomeViewModel homeViewModel, LoadViewModel loadViewModel, SettingsViewModel settingsViewModel, LogViewModel logViewModel, DiscordRPCManager discordRPCManager, RuntimeResource resource)
        {
            _discordRPCManager = discordRPCManager;

            _HomePage = homeViewModel;
            _LoadPage = loadViewModel;
            _SettingsPage = settingsViewModel;
            _LogPage = logViewModel;

            Resource = resource;

            Logger.Info(Resources.AppStart);

            _HomePage.ValidateFiles();

            this.WhenAnyValue(x => x.CurrentPage).Subscribe(page =>
            {
                HomeActive = page == _HomePage;
                LoadActive = page == _LoadPage;
                SettingsActive = page == _SettingsPage;
                LogActive = page == _LogPage;
            });

            selectedLang = Langs.FirstOrDefault(l => l.Key == Resource.Language, Langs[0]);

            OpenLinkCommand = ReactiveCommand.Create<string>(OpenLink);
            LangSelectionCommand = ReactiveCommand.Create<string>(Resource.SetLanguage);

            this.WhenAnyValue(x => x.selectedLang.Key)
                .Where(key => !string.IsNullOrEmpty(key))
                .ObserveOn(RxApp.MainThreadScheduler)
                .InvokeCommand(LangSelectionCommand);

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
