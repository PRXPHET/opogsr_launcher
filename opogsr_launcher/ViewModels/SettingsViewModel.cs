using Avalonia.Controls;
using opogsr_launcher.Other.Settings;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Reactive;

namespace opogsr_launcher.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        public ReactiveCommand<Button, Unit> SaveSettingsCommand { get; }

        private static void InitializeSettings()
        {
            Dictionary<SettingType, Dictionary<string, string>> settingsValues = [];

            foreach (SettingType t in Enum.GetValues<SettingType>())
            {
                var dict = SettingsLtxManager.Read(t);
                if (dict is not null)
                    settingsValues[t] = dict;
            }

            foreach (var page in SettingsPages.Pages)
            {
                if (settingsValues.Count != 0)
                {
                    var controls = page.Value.FindAll(st => st is not SettingsSeparator);
                    foreach (var control in controls)
                    {
                        if (settingsValues.TryGetValue(control.Type, out Dictionary<string, string> values) && values.TryGetValue(control.Command, out string val))
                            control.ChangeValue(val);
                    }
                }
            }
        }

        private void SaveSettings(Button btn)
        {
            Dictionary<SettingType, Dictionary<string, string>> settingsValues = [];

            foreach(SettingType t in Enum.GetValues<SettingType>())
                settingsValues[t] = [];

            foreach (var page in SettingsPages.Pages)
            {
                foreach (var st in page.Value)
                {
                    if (st is SettingsSeparator)
                        continue;

                    settingsValues[st.Type].Add(st.Command, st.GetValueAsString());
                }
            };

            foreach (var sv in settingsValues)
            {
                if (sv.Value.Count == 0)
                    continue;

                SettingsLtxManager.Write(sv.Key, sv.Value);
            }

            btn.IsEnabled = false;
        }

        public SettingsViewModel() 
        { 
            SaveSettingsCommand = ReactiveCommand.Create<Button>(SaveSettings);

            InitializeSettings();
        }
    }
}
