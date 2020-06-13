# ⚡ Lightweight.Caching

A collection of lightweight caching primitives.

LRU implementations are intended as an alternative to the System.Runtime.Caching.MemoryCache family of classes (e.g. HttpRuntime.Cache, System.Web.Caching et. al.). MemoryCache makes heap allocations when the native object key is not type string, and does not offer the fastest possible performance.

[![NuGet version](https://badge.fury.io/nu/Lightweight.Caching.svg)](https://badge.fury.io/nu/Lightweight.Caching)

# Overview

| Class |  Description |
|:-------|:---------|
| ClassicLru       | Bounded size LRU based with strict ordering.<br><br>Use if ordering is important, but data structures are synchronized with a lock which limits scalability. |
| ConcurrentLru       |  Bounded size pseudo LRU.<br><br>For when you   want a ConcurrentDictionary, but with bounded size. Maintains psuedo order, but is faster than ClassicLru and not prone to lock contention. |
| ConcurrentTlru        | Bounded size pseudo LRU, items have TTL.<br><br>Same as ConcurrentLru, but with a [time aware least recently used (TLRU)](https://en.wikipedia.org/wiki/Cache_replacement_policies#Time_aware_least_recently_used_(TLRU)) eviction policy. |
| FastConcurrentLru/FastConcurrentTLru      | Same as ConcurrentLru/ConcurrentTLru, but with hit counting logic eliminated making them between 10 and 30% faster.   |
| SingletonCache      | Cache singletons by key. Discard when no longer in use. <br><br> For example, cache a SemaphoreSlim per user, where user population is large, but active user count is low.   |
| Scoped<IDisposable>      | A threadsafe wrapper for storing IDisposable objects in a cache that may dispose and invalidate them. The scope keeps the object alive until all callers have finished.   |

# Performance

## Lru Benchmarks

### Lookup speed with queue cycling

Cache contains 6 items which are fetched repeatedly, no items are evicted. For LRU caches this measures time spent maintaining item access order. For all other classes (including MemoryCache), it is a pure lookup. FastConcurrentLru does not allocate and is approximately 10x faster than MemoryCache.

~~~
BenchmarkDotNet=v0.12.1, OS=Windows 10.0.18363.900 (1909/November2018Update/19H2)
Intel Core i7-5600U CPU 2.60GHz (Broadwell), 1 CPU, 4 logical and 2 physical cores
.NET Core SDK=3.1.301
  [Host]    : .NET Core 3.1.5 (CoreCLR 4.700.20.26901, CoreFX 4.700.20.27001), X64 RyuJIT
  RyuJitX64 : .NET Core 3.1.5 (CoreCLR 4.700.20.26901, CoreFX 4.700.20.27001), X64 RyuJIT

Job=RyuJitX64  Jit=RyuJit  Platform=X64
~~~

|                       Method |      Mean |    Error |   StdDev | Ratio |  Gen 0 | Allocated |
|----------------------------- |----------:|---------:|---------:|------:|-------:|----------:|
| ConcurrentDictionaryGetOrAdd |  15.94 ns | 0.308 ns | 0.342 ns |  1.00 |      - |         - |
|    FastConcurrentLruGetOrAdd |  22.01 ns | 0.427 ns | 0.555 ns |  1.39 |      - |         - |
|        ConcurrentLruGetOrAdd |  32.98 ns | 0.643 ns | 1.038 ns |  2.06 |      - |         - |
|   FastConcurrentTLruGetOrAdd | 120.79 ns | 2.247 ns | 3.994 ns |  7.59 |      - |         - |
|       ConcurrentTLruGetOrAdd | 136.76 ns | 2.619 ns | 3.497 ns |  8.60 |      - |         - |
|           ClassicLruGetOrAdd |  62.69 ns | 1.054 ns | 0.880 ns |  3.93 |      - |         - |
|      MemoryCacheGetStringKey | 273.82 ns | 2.970 ns | 2.319 ns | 17.14 | 0.0153 |      32 B |

## Meta-programming using structs for JIT dead code removal and inlining

TemplateConcurrentLru features injectable policies defined as structs. Since structs are subject to special JIT optimizations, the implementation is much faster than if these policies were defined as classes. Using this technique, 'Fast' versions without hit counting are within 30% of the speed of a ConcurrentDictionary.

Since DateTime.UtcNow is around 4x slower than a ConcurrentDictionary lookup, policies that involve time based expiry are significantly slower. Since these are injected as structs and the slow code is optimized away, it is possible maintain the fastest possible speed for the non-TTL policy.
