using System;
using System.Runtime.CompilerServices;

namespace BitFaster.Caching
{
    internal static class Ex
    {
        public static void ThrowArgNull(string paramName) => throw CreateArgumentNullException(paramName);

        public static void ThrowArgOutOfRange(string paramName) => throw CreateArgumentOutOfRangeException(paramName);

        public static void ThrowArgOutOfRange(string paramName, string message) => throw CreateArgumentOutOfRangeException(paramName, message);

        public static void ThrowInvalidOp() => throw CreateInvalidOperationException();

        public static void ThrowInvalidOp(string message) => throw CreateInvalidOperationException(message);

        public static void ThrowDisposed(string objectName) => throw CreateObjectDisposedException(objectName);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ArgumentNullException CreateArgumentNullException(string paramName) => new ArgumentNullException(paramName);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ArgumentOutOfRangeException CreateArgumentOutOfRangeException(string paramName) => new ArgumentOutOfRangeException(paramName);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ArgumentOutOfRangeException CreateArgumentOutOfRangeException(string paramName, string message) => new ArgumentOutOfRangeException(paramName, message);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static InvalidOperationException CreateInvalidOperationException() => new InvalidOperationException();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static InvalidOperationException CreateInvalidOperationException(string message) => new InvalidOperationException(message);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ObjectDisposedException CreateObjectDisposedException(string message) => new ObjectDisposedException(message);
    }
}
