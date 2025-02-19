namespace BitFaster.Caching
{
    internal class Padding
    {
#if TARGET_ARM64 || TARGET_LOONGARCH64
        internal const int CACHE_LINE_SIZE = 128;
#else
        internal const int CACHE_LINE_SIZE = 64;
#endif
    }
}
