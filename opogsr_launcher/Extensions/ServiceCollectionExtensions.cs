using Microsoft.Extensions.DependencyInjection;
using opogsr_launcher.Managers;
using opogsr_launcher.ViewModels;
using opogsr_launcher.Views;

namespace opogsr_launcher.Extensions
{
    internal static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddManagers(this IServiceCollection collection)
        {
            collection.AddSingleton<DiscordRPCManager>();
            collection.AddSingleton<GithubManager>();

            return collection;
        }

        public static IServiceCollection AddViewModels(this IServiceCollection collection)
        {
            collection.AddSingleton<MainViewModel>();
            collection.AddSingleton<HomeViewModel>();
            collection.AddSingleton<LoadViewModel>();
            collection.AddSingleton<SettingsViewModel>();
            collection.AddSingleton<LogViewModel>();

            return collection;
        }

        public static IServiceCollection AddViews(this IServiceCollection collection)
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
