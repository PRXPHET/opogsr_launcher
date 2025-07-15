using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace opogsr_launcher.Extensions
{
    public static class TaskExtensions
    {
        public static IEnumerable<Task<R>> WithMaxConcurrency<T, R>(this IEnumerable<T> tasks, SemaphoreSlim maxOperations, Func<T, Task<R>> functor)
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

        public static IEnumerable<Task> WithMaxConcurrency<T>(this IEnumerable<T> tasks, SemaphoreSlim maxOperations, Func<T, Task> functor, object v)
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
