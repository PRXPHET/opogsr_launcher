using Avalonia.Controls;
using Avalonia.Controls.Templates;
using opogsr_launcher.ViewModels;
using opogsr_launcher.Views;
using ReactiveUI;
using System;
using System.Collections.Generic;

namespace opogsr_launcher
{
    public class DataTemplateLocator : IDataTemplate
    {
        private static readonly Dictionary<Type, Func<Control>> Factory = new()
        {
            { typeof(MainViewModel), () => new MainView() },
            { typeof(HomeViewModel), () => new HomeView() },
            { typeof(LoadViewModel), () => new LoadView() },
            { typeof(SettingsViewModel), () => new SettingsView() },
            { typeof(LogViewModel), () => new LogView() }
        };

        public Control? Build(object? data)
        {
            if (data is null)
            {
                return new TextBlock { Text = "data was null" };
            }

            // Получаем фабрику для создания View
            if (Factory.TryGetValue(data.GetType(), out var factory))
            {
                var control = factory();
                control.DataContext = data;
                return control;
            }

            // Если тип не найден
            return new TextBlock { Text = $"Not Found: {data.GetType().FullName}" };
        }

        public bool Match(object? data) => data is ViewModelBase;
    }
}
