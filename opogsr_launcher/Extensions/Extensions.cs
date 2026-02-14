using Microsoft.Extensions.DependencyInjection;
using opogsr_launcher.Managers;
using opogsr_launcher.ViewModels;
using opogsr_launcher.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace opogsr_launcher.Extensions
{
    public static class Extensions
    {
        private const string BytesBad = "0B";

        private static readonly string[] BytesSuffixes = ["B", "KB", "MB", "GB", "TB", "PB", "EB"];

        extension(Convert)
        {
            public static string BytesToString(ulong bytes)
            {
                if (bytes == 0)
                    return BytesBad;

                int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
                double num = Math.Round(bytes / Math.Pow(1024, place), 1);
                return num.ToString() + BytesSuffixes[place];
            }
        }

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

        extension<T>(IEnumerable<T> tasks)
        {
            public IEnumerable<Task<R>> WithMaxConcurrency<R>(SemaphoreSlim maxOperations, Func<T, Task<R>> functor)
            {
                return tasks.Select(async task =>
                {
                    await maxOperations.WaitAsync();
                    try
                    {
                        return await functor(task);
                    }
                    finally { maxOperations.Release(); }
                });
            }

            public IEnumerable<Task> WithMaxConcurrency(SemaphoreSlim maxOperations, Func<T, Task> functor, object v)
            {
                return tasks.Select(async task =>
                {
                    await maxOperations.WaitAsync();
                    try
                    {
                        await functor(task);
                    }
                    finally { maxOperations.Release(); }
                });
            }
        }
    }
}
