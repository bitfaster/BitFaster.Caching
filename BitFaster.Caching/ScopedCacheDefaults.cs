
namespace BitFaster.Caching
{
    internal static class ScopedCacheDefaults
    {
        internal const int MaxRetry = 64;
        internal static readonly string RetryFailureMessage = $"Exceeded {MaxRetry} attempts to create a lifetime.";
    }
}
