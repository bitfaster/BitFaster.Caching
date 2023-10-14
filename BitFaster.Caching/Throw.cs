using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace BitFaster.Caching
{
    internal static class Throw
    {
#if NETCOREAPP3_0_OR_GREATER
        [DoesNotReturn]
#endif
        public static void ArgNull(ExceptionArgument arg) => throw CreateArgumentNullException(arg);

#if NETCOREAPP3_0_OR_GREATER
        [DoesNotReturn]
#endif
        public static void ArgOutOfRange(string paramName) => throw CreateArgumentOutOfRangeException(paramName);

#if NETCOREAPP3_0_OR_GREATER
        [DoesNotReturn]
#endif
        public static void ArgOutOfRange(string paramName, string message) => throw CreateArgumentOutOfRangeException(paramName, message);

        [ExcludeFromCodeCoverage]
#if NETCOREAPP3_0_OR_GREATER
        [DoesNotReturn]
#endif
        public static void InvalidOp(string message) => throw CreateInvalidOperationException(message);

#if NETCOREAPP3_0_OR_GREATER
        [DoesNotReturn]
#endif
        public static void ScopedRetryFailure() => throw CreateScopedRetryFailure();

#if NETCOREAPP3_0_OR_GREATER
        [DoesNotReturn]
#endif
        public static void Disposed<T>() => throw CreateObjectDisposedException<T>();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ArgumentNullException CreateArgumentNullException(ExceptionArgument arg) => new ArgumentNullException(GetArgumentString(arg));

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ArgumentOutOfRangeException CreateArgumentOutOfRangeException(string paramName) => new ArgumentOutOfRangeException(paramName);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ArgumentOutOfRangeException CreateArgumentOutOfRangeException(string paramName, string message) => new ArgumentOutOfRangeException(paramName, message);

        [ExcludeFromCodeCoverage]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static InvalidOperationException CreateInvalidOperationException(string message) => new InvalidOperationException(message);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static InvalidOperationException CreateScopedRetryFailure() => new InvalidOperationException(ScopedCacheDefaults.RetryFailureMessage);
        
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ObjectDisposedException CreateObjectDisposedException<T>() => new ObjectDisposedException(typeof(T).Name);

        [ExcludeFromCodeCoverage]
        private static string GetArgumentString(ExceptionArgument argument)
        {
            switch (argument)
            {
                case ExceptionArgument.cache: return nameof(ExceptionArgument.cache);
                case ExceptionArgument.comparer: return nameof(ExceptionArgument.comparer);
                case ExceptionArgument.scoped: return nameof(ExceptionArgument.scoped);
                case ExceptionArgument.capacity: return nameof(ExceptionArgument.capacity);
                case ExceptionArgument.node: return nameof(ExceptionArgument.node);
                default:
                    Debug.Fail("The ExceptionArgument value is not defined.");
                    return string.Empty;
            }
        }
    }

    internal enum ExceptionArgument
    {
        cache,
        comparer,
        scoped,
        capacity,
        node,
    }
}
