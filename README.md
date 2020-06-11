# ⚡ Lightweight.Caching

A lightweight high performance caching library with simple cache primitives suitable for use on the hot path.

## Meta-programming using struct for JIT dead code removal and inlining

ConcurrentLru features injectable policies defined as structs. Since structs are subject to special optimizations by the JITter, the implementation is much faster than if these policies were defined as classes. Using this technique, lookups without TTL are within 15% of the speed of a ConcurrentDictionary.

Since DateTime.UtcNow is around 4x slower than a ConcurrentDictionary lookup, policies that involve time based expiry are significantly slower. Since these are injected as structs and the slow code is optimized away, it is possible maintain the fastest possible speed for the non-TTL policy.

## Perf numbers

~~~
BenchmarkDotNet=v0.12.1, OS=Windows 10.0.18363.900 (1909/November2018Update/19H2)
Intel Core i7-5600U CPU 2.60GHz (Broadwell), 1 CPU, 4 logical and 2 physical cores
  [Host]    : .NET Framework 4.8 (4.8.4180.0), X86 LegacyJIT
  RyuJitX64 : .NET Framework 4.8 (4.8.4180.0), X64 RyuJIT

Job=RyuJitX64  Jit=RyuJit  Platform=X64
~~~

|                        Method |      Mean |    Error |   StdDev | Ratio | RatioSD |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|------------------------------ |----------:|---------:|---------:|------:|--------:|-------:|------:|------:|----------:|
|            DictionaryGetOrAdd |  18.65 ns | 0.384 ns | 0.341 ns |  1.00 |    0.00 |      - |     - |     - |         - |
|               DateTime.UtcNow |  76.31 ns | 1.373 ns | 2.805 ns |  4.07 |    0.18 |      - |     - |     - |         - |
|          SegmentedLruGetOrAdd |  21.62 ns | 0.262 ns | 0.218 ns |  1.16 |    0.03 |      - |     - |     - |         - |
|      ClassNoTtlPolicyGetOrAdd | 370.29 ns | 6.435 ns | 5.373 ns | 19.89 |    0.50 | 0.0458 |     - |     - |      96 B |
|    ConcurrentLruTemplGetOrAdd |  20.95 ns | 0.229 ns | 0.178 ns |  1.13 |    0.02 |      - |     - |     - |         - |
| ConcurrentLruTemplHitGetOrAdd |  36.37 ns | 0.340 ns | 0.284 ns |  1.95 |    0.04 |      - |     - |     - |         - |
| ConcurrentLruNoExpireGetOrAdd |  21.00 ns | 0.332 ns | 0.294 ns |  1.13 |    0.03 |      - |     - |     - |         - |
|   ConcurrentLruExpireGetOrAdd | 100.37 ns | 1.620 ns | 1.516 ns |  5.39 |    0.13 |      - |     - |     - |         - |
