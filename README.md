# ⚡ Lightweight.Caching

A collection of lightweight caching primitives suitable for use on the hot path. Intended as an alternative to the System.Runtime.Caching.MemoryCache family of classes (e.g. HttpRuntime.Cache, System.Web.Caching et. al.), which cause heap allocations when the native object key is not type string.

## Meta-programming using structs for JIT dead code removal and inlining

ConcurrentLru features injectable policies defined as structs. Since structs are subject to special optimizations by the JITter, the implementation is much faster than if these policies were defined as classes. Using this technique, lookups without TTL are within 15% of the speed of a ConcurrentDictionary.

Since DateTime.UtcNow is around 4x slower than a ConcurrentDictionary lookup, policies that involve time based expiry are significantly slower. Since these are injected as structs and the slow code is optimized away, it is possible maintain the fastest possible speed for the non-TTL policy.

## Performance

~~~
BenchmarkDotNet=v0.12.1, OS=Windows 10.0.18363.900 (1909/November2018Update/19H2)
Intel Core i7-5600U CPU 2.60GHz (Broadwell), 1 CPU, 4 logical and 2 physical cores
.NET Core SDK=3.1.301
  [Host]    : .NET Core 3.1.5 (CoreCLR 4.700.20.26901, CoreFX 4.700.20.27001), X64 RyuJIT
  RyuJitX64 : .NET Core 3.1.5 (CoreCLR 4.700.20.26901, CoreFX 4.700.20.27001), X64 RyuJIT

Job=RyuJitX64  Jit=RyuJit  Platform=X64
~~~

|                        Method |      Mean |    Error |   StdDev | Ratio | RatioSD |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|------------------------------ |----------:|---------:|---------:|------:|--------:|-------:|------:|------:|----------:|
|            DictionaryGetOrAdd |  16.40 ns | 0.309 ns | 0.274 ns |  1.00 |    0.00 |      - |     - |     - |         - |
|                DateTimeUtcNow |  90.70 ns | 0.781 ns | 0.693 ns |  5.53 |    0.10 |      - |     - |     - |         - |
|                MemoryCacheGet | 365.01 ns | 3.050 ns | 2.547 ns | 22.23 |    0.45 | 0.0610 |     - |     - |     128 B |
|          SegmentedLruGetOrAdd |  22.30 ns | 0.213 ns | 0.178 ns |  1.36 |    0.03 |      - |     - |     - |         - |
|      ClassNoTtlPolicyGetOrAdd |  66.22 ns | 1.347 ns | 2.595 ns |  4.10 |    0.21 | 0.0459 |     - |     - |      96 B |
|    ConcurrentLruTemplGetOrAdd |  22.16 ns | 0.470 ns | 0.895 ns |  1.37 |    0.07 |      - |     - |     - |         - |
| ConcurrentLruTemplHitGetOrAdd |  34.68 ns | 0.705 ns | 1.033 ns |  2.14 |    0.07 |      - |     - |     - |         - |
|         ConcurrentLruGetOrAdd |  21.88 ns | 0.449 ns | 0.441 ns |  1.34 |    0.04 |      - |     - |     - |         - |
|   ConcurrentLruExpireGetOrAdd | 115.64 ns | 2.346 ns | 2.967 ns |  7.01 |    0.17 |      - |     - |     - |         - |
