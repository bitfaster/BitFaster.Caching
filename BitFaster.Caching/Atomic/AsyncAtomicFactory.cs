using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching.Atomic
{
    /// <summary>
    /// A class that provides simple, lightweight exactly once initialization for values
    /// stored in a cache.
    /// </summary>
    /// <typeparam name="K">The type of the key.</typeparam>
    /// <typeparam name="V">The type of the value.</typeparam>
    [DebuggerDisplay("IsValueCreated={IsValueCreated}, Value={ValueIfCreated}")]
    public sealed class AsyncAtomicFactory<K, V> : IEquatable<AsyncAtomicFactory<K, V>>
        where K : notnull
    {
        private Initializer? initializer;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private V? value;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncAtomicFactory{K, V}"/> class.
        /// </summary>
        public AsyncAtomicFactory()
        {
            initializer = new Initializer();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncAtomicFactory{K, V}"/> class with the
        /// specified value.
        /// </summary>
        /// <param name="value">The value.</param>
        public AsyncAtomicFactory(V value)
        {
            this.value = value;
        }

        /// <summary>
        /// Gets the value. If <see cref="IsValueCreated"/> is false, calling <see cref="GetValueAsync"/> will force initialization via the <paramref name="valueFactory"/> parameter.
        /// </summary>
        /// <param name="key">The key associated with the value.</param>
        /// <param name="valueFactory">The value factory to use to create the value when it is not initialized.</param>
        /// <returns>The value.</returns>
        public async ValueTask<V> GetValueAsync(K key, Func<K, Task<V>> valueFactory)
        {
            if (initializer == null)
            {
                return value!;
            }

            return await CreateValueAsync(key, new AsyncValueFactory<K, V>(valueFactory)).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the value. If <see cref="IsValueCreated"/> is false, calling <see cref="GetValueAsync{TArg}"/> will force initialization via the <paramref name="valueFactory"/> parameter.
        /// </summary>
        /// <typeparam name="TArg">The type of the value factory argument.</typeparam>
        /// <param name="key">The key associated with the value.</param>
        /// <param name="valueFactory">The value factory to use to create the value when it is not initialized.</param>
        /// <param name="factoryArgument">The value factory argument.</param>
        /// <returns>The value.</returns>
#if NET9_0_OR_GREATER
        public ValueTask<V> GetValueAsync<TArg>(K key, Func<K, TArg, Task<V>> valueFactory, TArg factoryArgument)
            where TArg : allows ref struct
#else
        public async ValueTask<V> GetValueAsync<TArg>(K key, Func<K, TArg, Task<V>> valueFactory, TArg factoryArgument)
#endif
        {
            if (initializer == null)
            {
                return new ValueTask<V>(value!);
            }

#if NET9_0_OR_GREATER
            return CreateValueAsync(key, new RefAsyncValueFactoryArg<K, TArg, V>(valueFactory, factoryArgument));
#else
            return await CreateValueAsync(key, new AsyncValueFactoryArg<K, TArg, V>(valueFactory, factoryArgument)).ConfigureAwait(false);
#endif
        }

        /// <summary>
        /// Gets a value indicating whether the value has been initialized.
        /// </summary>
        public bool IsValueCreated => initializer == null;

        /// <summary>
        /// Gets the value if it has been initialized, else default.
        /// </summary>
        public V? ValueIfCreated
        {
            get
            {
                if (!IsValueCreated)
                {
                    return default;
                }

                return value;
            }
        }

        ///<inheritdoc/>
        public override bool Equals(object? obj)
        {
            return Equals(obj as AsyncAtomicFactory<K, V>);
        }

        ///<inheritdoc/>
        public bool Equals(AsyncAtomicFactory<K, V>? other)
        {
            if (other is null || !IsValueCreated || !other.IsValueCreated)
            {
                return false;
            }

            return EqualityComparer<V>.Default.Equals(ValueIfCreated, other.ValueIfCreated);
        }

        ///<inheritdoc/>
        public override int GetHashCode()
        {
            if (!IsValueCreated)
            {
                return 0;
            }

            return ValueIfCreated!.GetHashCode();
        }

        private ValueTask<V> CreateValueAsync<TFactory>(K key, TFactory valueFactory)
#if NET9_0_OR_GREATER
            where TFactory : struct, IAsyncValueFactory<K, V>, allows ref struct
#else
            where TFactory : struct, IAsyncValueFactory<K, V>
#endif
        {
            var init = Volatile.Read(ref initializer);

            if (init != null)
            {
                var createdValue = init.CreateValueAsync(key, valueFactory);

                if (createdValue.IsCompletedSuccessfully)
                {
                    value = createdValue.Result;
                    Volatile.Write(ref initializer, null);
                }
                else
                {
                    return AwaitCreatedValueAsync(createdValue);
                }
            }

            return new ValueTask<V>(value!);
        }

        private async ValueTask<V> AwaitCreatedValueAsync(ValueTask<V> createdValue)
        {
            value = await createdValue.ConfigureAwait(false);
            Volatile.Write(ref initializer, null);
            return value!;
        }

        private class Initializer
        {
            private bool isInitialized;
            private Task<V>? valueTask;

            public ValueTask<V> CreateValueAsync<TFactory>(K key, TFactory valueFactory)
#if NET9_0_OR_GREATER
                where TFactory : struct, IAsyncValueFactory<K, V>, allows ref struct
#else
                where TFactory : struct, IAsyncValueFactory<K, V>
#endif
            {
                var tcs = new TaskCompletionSource<V>(TaskCreationOptions.RunContinuationsAsynchronously);

                var synchronizedTask = DoubleCheck(tcs.Task);

                if (ReferenceEquals(synchronizedTask, tcs.Task))
                {
                    Task<V> factoryTask;

                    try
                    {
                        factoryTask = valueFactory.CreateAsync(key);
                    }
                    catch (Exception ex)
                    {
                        Volatile.Write(ref isInitialized, false);
                        tcs.SetException(ex);
                        return new ValueTask<V>(tcs.Task);
                    }

                    return CompleteSynchronizedTaskAsync(factoryTask, tcs);
                }

                return new ValueTask<V>(synchronizedTask);
            }

            private async ValueTask<V> CompleteSynchronizedTaskAsync(Task<V> factoryTask, TaskCompletionSource<V> tcs)
            {
                try
                {
                    var createdValue = await factoryTask.ConfigureAwait(false);
                    tcs.SetResult(createdValue);
                    return createdValue;
                }
                catch (Exception ex)
                {
                    Volatile.Write(ref isInitialized, false);
                    tcs.SetException(ex);

                    // always await the task to avoid unobserved task exceptions - normal case is that no other thread is waiting.
                    // this will re-throw the exception.
                    return await tcs.Task.ConfigureAwait(false);
                }
            }

#pragma warning disable CA2002 // Do not lock on objects with weak identity
            private Task<V> DoubleCheck(Task<V> value)
            {
                // Fast path
                if (Volatile.Read(ref isInitialized))
                {
                    return valueTask!;
                }

                lock (this)
                {
                    if (!isInitialized)
                    {
                        valueTask = value;
                        isInitialized = true;
                    }
                }

                return valueTask!;
            }
#pragma warning restore CA2002 // Do not lock on objects with weak identity
        }
    }
}
