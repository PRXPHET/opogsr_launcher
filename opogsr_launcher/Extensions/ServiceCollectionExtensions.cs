using Microsoft.Extensions.DependencyInjection;
using opogsr_launcher.Managers;
using opogsr_launcher.ViewModels;
using opogsr_launcher.Views;

namespace opogsr_launcher.Extensions
{
    internal static class ServiceCollectionExtensions
    {
        extension(IServiceCollection collection)
        {
            public IServiceCollection AddManagers()
            {
                collection.AddSingleton<DiscordRPCManager>();

                return collection;
            }

            public IServiceCollection AddViewModels()
            {
                collection.AddSingleton<MainViewModel>();
                collection.AddSingleton<HomeViewModel>();
                collection.AddSingleton<LoadViewModel>();
                collection.AddSingleton<SettingsViewModel>();
                collection.AddSingleton<LogViewModel>();

                return collection;
            }

            public IServiceCollection AddViews()
            {
                collection.AddSingleton<MainView>();
                collection.AddSingleton<HomeView>();
                collection.AddSingleton<LoadView>();
                collection.AddSingleton<SettingsView>();
                collection.AddSingleton<LogView>();

                return collection;
            }
        }
    }
}
