using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace BitFaster.Caching.Atomic
{
    /// <summary>
    /// A class that provides simple, lightweight exactly once initialization for values stored
    /// in a cache. Exceptions are propogated to the caller.
    /// </summary>
    /// <typeparam name="K">The type of the key.</typeparam>
    /// <typeparam name="V">The type of the value.</typeparam>
    [DebuggerDisplay("IsValueCreated={IsValueCreated}, Value={ValueIfCreated}")]
    public sealed class AtomicFactory<K, V> : IEquatable<AtomicFactory<K, V>>
        where K : notnull
    {
        private Initializer initializer;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private V value;

        /// <summary>
        /// Initializes a new instance of the <see cref="AtomicFactory{K, V}"/> class.
        /// </summary>
        public AtomicFactory()
        {
            initializer = new Initializer();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AtomicFactory{K, V}"/> class with the
        /// specified value.
        /// </summary>
        /// <param name="value">The value.</param>
        public AtomicFactory(V value)
        {
            this.value = value;
        }

        /// <summary>
        /// Gets the value. If <see cref="IsValueCreated"/> is false, calling <see cref="GetValue"/> will force initialization via the <paramref name="valueFactory"/> parameter.
        /// </summary>
        /// <param name="key">The key associated with the value.</param>
        /// <param name="valueFactory">The value factory to use to create the value when it is not initialized.</param>
        /// <returns>The value.</returns>
        public V GetValue(K key, Func<K, V> valueFactory)
        {
            if (initializer == null)
            {
                return value;
            }

            return CreateValue(key, new ValueFactory<K, V>(valueFactory));
        }

        /// <summary>
        /// Gets the value. If <see cref="IsValueCreated"/> is false, calling <see cref="GetValue{TArg}"/> will force initialization via the <paramref name="valueFactory"/> parameter.
        /// </summary>
        /// <typeparam name="TArg">The type of the value factory argument.</typeparam>
        /// <param name="key">The key associated with the value.</param>
        /// <param name="valueFactory">The value factory to use to create the value when it is not initialized.</param>
        /// <param name="factoryArgument">The value factory argument.</param>
        /// <returns>The value.</returns>
        public V GetValue<TArg>(K key, Func<K, TArg, V> valueFactory, TArg factoryArgument)
        {
            if (initializer == null)
            {
                return value;
            }

            return CreateValue(key, new ValueFactoryArg<K, TArg, V>(valueFactory, factoryArgument));
        }

        /// <summary>
        /// Gets a value indicating whether the value has been initialized.
        /// </summary>
        public bool IsValueCreated => Volatile.Read(ref initializer) == null;

        /// <summary>
        /// Gets the value if it has been initialized, else default.
        /// </summary>
        public V ValueIfCreated
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

        /// <summary>
        /// Note the failure case works like this:
        /// 1. Thread A enters AtomicFactory.CreateValue then Initializer.CreateValue and holds the lock.
        /// 2. Thread B enters AtomicFactory.CreateValue then Initializer.CreateValue and queues on the lock.
        /// 3. Thread A calls value factory, and after 1 second throws an exception. The exception is 
        /// captured in exceptionDispatch, lock is released, and an exeption is thrown.
        /// 4. AtomicFactory.CreateValue catches the exception and creates a fresh initializer.
        /// 5. Thread B enters the lock, finds exceptionDispatch is populated and immediately throws.
        /// 6. Thread C can now start from a clean state.
        /// This mitigates lock convoys where many queued threads will fail slowly one by one, introducing delays
        /// and multiplying the number of calls to the failing resource.
        /// </summary>
        private V CreateValue<TFactory>(K key, TFactory valueFactory) where TFactory : struct, IValueFactory<K, V>
        {
            var init = Volatile.Read(ref initializer);

            if (init != null)
            {
                try
                {
                    value = init.CreateValue(key, valueFactory);
                    Volatile.Write(ref initializer, null); // volatile write must occur after setting value
                }
                catch
                {
                    // Overwrite the initializer with a fresh copy. New threads will start from a clean state.
                    Volatile.Write(ref initializer, new Initializer());
                    throw;
                }
            }

            return value;
        }

        ///<inheritdoc/>
        public override bool Equals(object obj)
        {
            return Equals(obj as AtomicFactory<K, V>);
        }

        ///<inheritdoc/>
        public bool Equals(AtomicFactory<K, V> other)
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

            return ValueIfCreated.GetHashCode();
        }

#pragma warning disable CA2002 // Do not lock on objects with weak identity
        private class Initializer
        {
            private bool isInitialized;
            private V value;
            private ExceptionDispatchInfo exceptionDispatch;

            public V CreateValue<TFactory>(K key, TFactory valueFactory) where TFactory : struct, IValueFactory<K, V>
            {
                lock (this)
                {
                    if (isInitialized)
                    {
                        return value;
                    }

                    // If a previous thread called the factory and failed, throw the same error instead
                    // of calling the factory again.
                    if (exceptionDispatch != null)
                        exceptionDispatch.Throw();

                    try
                    {
                        value = valueFactory.Create(key);
                        isInitialized = true;
                        return value;
                    }
                    catch (Exception ex)
                    {
                        exceptionDispatch = ExceptionDispatchInfo.Capture(ex);
                        throw;
                    }
                }
            }
        }
#pragma warning restore CA2002 // Do not lock on objects with weak identity
    }
}
