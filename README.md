# ⚡ Lightweight.Caching

A collection of lightweight caching primitives.

LRU implementations are intended as an alternative to the System.Runtime.Caching.MemoryCache family of classes (e.g. HttpRuntime.Cache, System.Web.Caching et. al.), which cause heap allocations when the native object key is not type string.

# Overview

| Class | Description | Example use |
|:-------|:-------------|:-------------|
| ClassicLru      | Bounded size LRU based on a linked list and dictionary. | If strict ordering is important, but data structures are synchronized with a global lock which limits scalability. |
| ConcurrentLru      | Bounded size pseudo LRU, with LRU and TLRU policies. | Maintains psuedo order, but is faster than ClassicLru and not prone to lock contention. |
| SingletonCache      | Cache singletons by key. Discard when not in use. | Cache a semaphore per user, where user population is large, but active user count is low.   |

# Performance

## Meta-programming using structs for JIT dead code removal and inlining

ConcurrentLru features injectable policies defined as structs. Since structs are subject to special optimizations by the JITter, the implementation is much faster than if these policies were defined as classes. Using this technique, lookups without TTL are within 15% of the speed of a ConcurrentDictionary.

Since DateTime.UtcNow is around 4x slower than a ConcurrentDictionary lookup, policies that involve time based expiry are significantly slower. Since these are injected as structs and the slow code is optimized away, it is possible maintain the fastest possible speed for the non-TTL policy.

## ConcurrentLru Benchmarks

### Lookup speed

The cache contains a single item which is repeatedly fetched. Results represent raw lookup speed.

~~~
BenchmarkDotNet=v0.12.1, OS=Windows 10.0.18363.900 (1909/November2018Update/19H2)
Intel Core i7-5600U CPU 2.60GHz (Broadwell), 1 CPU, 4 logical and 2 physical cores
.NET Core SDK=3.1.301
  [Host]    : .NET Core 3.1.5 (CoreCLR 4.700.20.26901, CoreFX 4.700.20.27001), X64 RyuJIT
  RyuJitX64 : .NET Core 3.1.5 (CoreCLR 4.700.20.26901, CoreFX 4.700.20.27001), X64 RyuJIT

Job=RyuJitX64  Jit=RyuJit  Platform=X64
~~~

|                        Method |      Mean |    Error |    StdDev |    Median | Ratio | RatioSD |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|------------------------------ |----------:|---------:|----------:|----------:|------:|--------:|-------:|------:|------:|----------:|
|            DictionaryGetOrAdd |  16.33 ns | 0.292 ns |  0.273 ns |  16.28 ns |  1.00 |    0.00 |      - |     - |     - |         - |
|                DateTimeUtcNow |  90.85 ns | 0.810 ns |  0.718 ns |  90.76 ns |  5.57 |    0.09 |      - |     - |     - |         - |
|          MemoryCacheGetIntKey | 312.10 ns | 7.512 ns | 21.433 ns | 304.44 ns | 18.86 |    1.08 | 0.0153 |     - |     - |      32 B |
|       MemoryCacheGetStringKey | 281.65 ns | 5.629 ns | 14.830 ns | 277.49 ns | 17.59 |    1.06 | 0.0153 |     - |     - |      32 B |
|          SegmentedLruGetOrAdd |  24.73 ns | 0.523 ns |  0.943 ns |  24.47 ns |  1.51 |    0.07 |      - |     - |     - |         - |
|      ClassNoTtlPolicyGetOrAdd |  69.11 ns | 1.406 ns |  3.231 ns |  68.86 ns |  4.24 |    0.22 | 0.0459 |     - |     - |      96 B |
|    ConcurrentLruTemplGetOrAdd |  23.38 ns | 0.333 ns |  0.295 ns |  23.42 ns |  1.43 |    0.03 |      - |     - |     - |         - |
| ConcurrentLruTemplHitGetOrAdd |  35.03 ns | 0.723 ns |  1.227 ns |  35.18 ns |  2.20 |    0.08 |      - |     - |     - |         - |
|         ConcurrentLruGetOrAdd |  21.95 ns | 0.473 ns |  1.048 ns |  21.73 ns |  1.35 |    0.05 |      - |     - |     - |         - |
|   ConcurrentLruExpireGetOrAdd | 117.02 ns | 2.187 ns |  2.046 ns | 116.68 ns |  7.17 |    0.16 |      - |     - |     - |         - |

### Lookup speed with queue cycling

Cache contains 6 items, no items are evicted. The LRU caches maintaining item access order.

~~~
BenchmarkDotNet=v0.12.1, OS=Windows 10.0.18363.900 (1909/November2018Update/19H2)
Intel Core i7-5600U CPU 2.60GHz (Broadwell), 1 CPU, 4 logical and 2 physical cores
.NET Core SDK=3.1.301
  [Host]    : .NET Core 3.1.5 (CoreCLR 4.700.20.26901, CoreFX 4.700.20.27001), X64 RyuJIT
  RyuJitX64 : .NET Core 3.1.5 (CoreCLR 4.700.20.26901, CoreFX 4.700.20.27001), X64 RyuJIT

Job=RyuJitX64  Jit=RyuJit  Platform=X64
~~~

|                       Method |      Mean |    Error |   StdDev | Ratio | RatioSD |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|----------------------------- |----------:|---------:|---------:|------:|--------:|-------:|------:|------:|----------:|
| ConcurrentDictionaryGetOrAdd |  16.06 ns | 0.311 ns | 0.370 ns |  1.00 |    0.00 |      - |     - |     - |         - |
|      MemoryCacheGetStringKey | 272.16 ns | 1.708 ns | 1.427 ns | 16.93 |    0.44 | 0.0153 |     - |     - |      32 B |
|           ClassicLruGetOrAdd |  65.74 ns | 1.101 ns | 0.976 ns |  4.08 |    0.13 |      - |     - |     - |         - |
|        ConcurrentLruGetOrAdd |  22.01 ns | 0.251 ns | 0.210 ns |  1.37 |    0.04 |      - |     - |     - |         - |