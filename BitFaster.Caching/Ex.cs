using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace BitFaster.Caching
{
    internal static class Ex
    {
        public static void ThrowArgNull(ExceptionArgument arg) => throw CreateArgumentNullException(arg);

        public static void ThrowArgOutOfRange(string paramName) => throw CreateArgumentOutOfRangeException(paramName);

        public static void ThrowArgOutOfRange(string paramName, string message) => throw CreateArgumentOutOfRangeException(paramName, message);

        public static void ThrowInvalidOp() => throw CreateInvalidOperationException();

        public static void ThrowInvalidOp(string message) => throw CreateInvalidOperationException(message);

        public static void ThrowScopedRetryFailure() => throw CreateScopedRetryFailure();

        public static void ThrowDisposed<T>() => throw CreateObjectDisposedException<T>();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ArgumentNullException CreateArgumentNullException(ExceptionArgument arg) => new ArgumentNullException(GetArgumentString(arg));

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ArgumentOutOfRangeException CreateArgumentOutOfRangeException(string paramName) => new ArgumentOutOfRangeException(paramName);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ArgumentOutOfRangeException CreateArgumentOutOfRangeException(string paramName, string message) => new ArgumentOutOfRangeException(paramName, message);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static InvalidOperationException CreateInvalidOperationException() => new InvalidOperationException();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static InvalidOperationException CreateInvalidOperationException(string message) => new InvalidOperationException(message);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static InvalidOperationException CreateScopedRetryFailure() => new InvalidOperationException(ScopedCacheDefaults.RetryFailureMessage);
        
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ObjectDisposedException CreateObjectDisposedException<T>() => new ObjectDisposedException(nameof(T));

        private static string GetArgumentString(ExceptionArgument argument)
        {
            switch (argument)
            {
                case ExceptionArgument.cache: return nameof(ExceptionArgument.cache);
                case ExceptionArgument.comparer: return nameof(ExceptionArgument.comparer);
                case ExceptionArgument.scoped: return nameof(ExceptionArgument.scoped);
                case ExceptionArgument.capacity: return nameof(ExceptionArgument.capacity);
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
    }
}
