# ⚡ Lightweight.Caching

A collection of lightweight caching primitives.

LRU implementations are intended as an alternative to the System.Runtime.Caching.MemoryCache family of classes (e.g. HttpRuntime.Cache, System.Web.Caching et. al.). MemoryCache makes heap allocations when the native object key is not type string, and does not offer the fastest possible performance.

[![NuGet version](https://badge.fury.io/nu/Lightweight.Caching.svg)](https://badge.fury.io/nu/Lightweight.Caching)

# Overview

| Class |  Example use |
|:-------|:---------|
| ClassicLru       | Bounded size LRU based with strict ordering.<br><br>Use if ordering is important, but data structures are synchronized with a lock which limits scalability. |
| ConcurrentLru       |  Bounded size pseudo LRU.<br><br>For when you   want a ConcurrentDictionary, but with bounded size. Maintains psuedo order, but is faster than ClassicLru and not prone to lock contention. |
| ConcurrentTlru        | Bounded size pseudo LRU, items have TTL.<br><br>Same as ConcurrentLru, but with a [time aware least recently used (TLRU)](https://en.wikipedia.org/wiki/Cache_replacement_policies#Time_aware_least_recently_used_(TLRU)) eviction policy. |
| FastConcurrentLru/FastConcurrentTLru      | Same as ConcurrentLru/ConcurrentTLru, but with hit counting logic eliminated making them about 10% faster.   |
| SingletonCache      | Cache singletons by key. Discard when not in use. <br><br> Cache a semaphore per user, where user population is large, but active user count is low.   |

# Performance

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

|                       Method |      Mean |    Error |   StdDev | Ratio | RatioSD |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|----------------------------- |----------:|---------:|---------:|------:|--------:|-------:|------:|------:|----------:|
|           DictionaryGetOrAdd |  18.30 ns | 0.208 ns | 0.195 ns |  1.00 |    0.00 |      - |     - |     - |         - |
|               DateTimeUtcNow |  99.66 ns | 1.003 ns | 0.890 ns |  5.45 |    0.09 |      - |     - |     - |         - |
|         MemoryCacheGetIntKey | 324.41 ns | 5.246 ns | 4.651 ns | 17.73 |    0.36 | 0.0153 |     - |     - |      32 B |
|      MemoryCacheGetStringKey | 299.24 ns | 5.666 ns | 4.732 ns | 16.35 |    0.35 | 0.0153 |     - |     - |      32 B |
| ConcurrentLruNoCountGetOrAdd |  25.78 ns | 0.497 ns | 0.415 ns |  1.41 |    0.03 |      - |     - |     - |         - |
|        ConcurrentLruGetOrAdd |  33.72 ns | 0.669 ns | 0.559 ns |  1.84 |    0.03 |      - |     - |     - |         - |
|       ConcurrentTLruGetOrAdd | 137.25 ns | 2.713 ns | 2.538 ns |  7.50 |    0.18 |      - |     - |     - |         - |

### Lookup speed with queue cycling

Cache contains 6 items which are fetched repeatedly, no items are evicted. For LRU caches this measures time spent maintaining item access order.

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
| ConcurrentDictionaryGetOrAdd |  17.75 ns | 0.264 ns | 0.206 ns |  1.00 |    0.00 |      - |     - |     - |         - |
|      MemoryCacheGetStringKey | 303.91 ns | 5.963 ns | 5.578 ns | 17.07 |    0.41 | 0.0153 |     - |     - |      32 B |
|           ClassicLruGetOrAdd |  73.06 ns | 1.249 ns | 1.282 ns |  4.12 |    0.11 |      - |     - |     - |         - |
|        ConcurrentLruGetOrAdd |  35.00 ns | 0.452 ns | 0.377 ns |  1.97 |    0.03 |      - |     - |     - |         - |
|       ConcurrentTLruGetOrAdd | 143.92 ns | 2.776 ns | 2.727 ns |  8.09 |    0.14 |      - |     - |     - |         - |

## Meta-programming using structs for JIT dead code removal and inlining

ConcurrentLru features injectable policies defined as structs. Since structs are subject to special optimizations by the JITter, the implementation is much faster than if these policies were defined as classes. Using this technique, lookups without TTL are within 15% of the speed of a ConcurrentDictionary.

Since DateTime.UtcNow is around 4x slower than a ConcurrentDictionary lookup, policies that involve time based expiry are significantly slower. Since these are injected as structs and the slow code is optimized away, it is possible maintain the fastest possible speed for the non-TTL policy.
