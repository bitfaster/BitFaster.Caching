using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    [DebuggerDisplay("IsValueCreated={IsValueCreated}, Value={ValueIfCreated}")]
    public class AsyncAtomic<K, V>
    {
        private Initializer initializer;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private V value;

        public AsyncAtomic()
        {
            this.initializer = new Initializer();
        }

        public AsyncAtomic(V value)
        {
            this.value = value;
        }

        public V GetValue(K key, Func<K, V> valueFactory)
        {
            if (this.initializer == null)
            {
                return this.value;
            }

            return CreateValue(key, valueFactory);
        }

        public async Task<V> GetValueAsync(K key, Func<K, Task<V>> valueFactory)
        {
            if (this.initializer == null)
            {
                return this.value;
            }

            return await CreateValueAsync(key, valueFactory).ConfigureAwait(false);
        }

        public bool IsValueCreated => this.initializer == null;

        public V ValueIfCreated
        {
            get
            {
                if (!this.IsValueCreated)
                {
                    return default;
                }

                return this.value;
            }
        }

        private V CreateValue(K key, Func<K, V> valueFactory)
        {
            Initializer init = this.initializer;

            if (init != null)
            {
                this.value = init.CreateValue(key, valueFactory);
                this.initializer = null;
            }

            return this.value;
        }

        private async Task<V> CreateValueAsync(K key, Func<K, Task<V>> valueFactory)
        {
            Initializer init = this.initializer;

            if (init != null)
            {
                this.value = await init.CreateValueAsync(key, valueFactory).ConfigureAwait(false);
                this.initializer = null;
            }

            return this.value;
        }

        private class Initializer
        {
            private object syncLock = new object();
            private bool isInitialized;
            private Task<V> valueTask;

            public V CreateValue(K key, Func<K, V> valueFactory)
            {
                var tcs = new TaskCompletionSource<V>(TaskCreationOptions.RunContinuationsAsynchronously);

                var synchronizedTask = Synchronized.Initialize(ref this.valueTask, ref isInitialized, ref syncLock, tcs.Task);

                if (ReferenceEquals(synchronizedTask, tcs.Task))
                {
                    try
                    {
                        var value = valueFactory(key);
                        tcs.SetResult(value);
                        return value;
                    }
                    catch (Exception ex)
                    {
                        Volatile.Write(ref isInitialized, false);
                        tcs.SetException(ex);
                        throw;
                    }
                }

                // this isn't needed for .NET Core
                // https://stackoverflow.com/questions/53265020/c-sharp-async-await-deadlock-problem-gone-in-netcore
                return TaskSynchronization<V>.GetResult(synchronizedTask);
            }

            public async Task<V> CreateValueAsync(K key, Func<K, Task<V>> valueFactory)
            {
                var tcs = new TaskCompletionSource<V>(TaskCreationOptions.RunContinuationsAsynchronously);

                var synchronizedTask = Synchronized.Initialize(ref this.valueTask, ref isInitialized, ref syncLock, tcs.Task);

                if (ReferenceEquals(synchronizedTask, tcs.Task))
                {
                    try
                    {
                        var value = await valueFactory(key).ConfigureAwait(false);
                        tcs.SetResult(value);

                        return value;
                    }
                    catch (Exception ex)
                    {
                        Volatile.Write(ref isInitialized, false);
                        tcs.SetException(ex);
                        throw;
                    }
                }

                return await synchronizedTask.ConfigureAwait(false);
            }
        }
    }

    internal static class Synchronized
    {
        public static V Initialize<V>(ref V target, ref bool initialized, ref object syncLock, V value)
        {
            // Fast path
            if (Volatile.Read(ref initialized))
            {
                return target;
            }

            lock (syncLock)
            {
                if (!Volatile.Read(ref initialized))
                {
                    target = value;
                    Volatile.Write(ref initialized, true);
                }
            }

            return target;
        }
    }

    public static class TaskSynchronization<T>
    {
        private static ISynchronizationPolicy SynchronizationPolicy = new GetAwaiterPolicy();

        public static T GetResult(Task<T> task)
        {
            return SynchronizationPolicy.GetResult(task);
        }

        public static void GetResult(Task task)
        {
            SynchronizationPolicy.GetResult(task);
        }

        public static void UseTaskRun()
        {
            SynchronizationPolicy = new TaskRunPolicy();
        }

        public static void UseAwaiter()
        {
            SynchronizationPolicy = new GetAwaiterPolicy();
        }
    }

    internal interface ISynchronizationPolicy
    {
        T GetResult<T>(Task<T> task);

        void GetResult(Task task);
    }

    internal class GetAwaiterPolicy : ISynchronizationPolicy
    {
        public T GetResult<T>(Task<T> task)
        {
            return task.GetAwaiter().GetResult();
        }

        public void GetResult(Task task)
        {
            task.GetAwaiter().GetResult();
        }
    }

    internal class TaskRunPolicy : ISynchronizationPolicy
    {
        public T GetResult<T>(Task<T> task)
        {
            return Task.Run(async () => await task).Result;
        }

        public void GetResult(Task task)
        {
            Task.Run(async () => await task).Wait();
        }
    }
}
