# ⚡ BitFaster.Caching

High performance, thread safe in-memory caching primitives for .NET.

LRU implementations are intended as an alternative to the System.Runtime.Caching.MemoryCache family of classes (e.g. HttpRuntime.Cache, System.Web.Caching et. al.). MemoryCache makes heap allocations when the native object key is not type string, and does not offer the fastest possible performance.

[![NuGet version](https://badge.fury.io/nu/BitFaster.Caching.svg)](https://badge.fury.io/nu/BitFaster.Caching)

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

Benchmarks are based on BenchmarkDotNet, so are single threaded. The ConcurrentLru family of classes can outperform ClassicLru in multithreaded workloads.

~~~
BenchmarkDotNet=v0.12.1, OS=Windows 10.0.18363.900 (1909/November2018Update/19H2)
Intel Core i7-5600U CPU 2.60GHz (Broadwell), 1 CPU, 4 logical and 2 physical cores
.NET Core SDK=3.1.301
  [Host]    : .NET Core 3.1.5 (CoreCLR 4.700.20.26901, CoreFX 4.700.20.27001), X64 RyuJIT
  RyuJitX64 : .NET Core 3.1.5 (CoreCLR 4.700.20.26901, CoreFX 4.700.20.27001), X64 RyuJIT

Job=RyuJitX64  Jit=RyuJit  Platform=X64
~~~

### Lookup speed

Cache contains 6 items which are fetched repeatedly, no items are evicted. Representative of high hit rate scenario, when there are a low number of hot items.

- ConcurrentLru family does not move items in the queues, it is just marking as accessed for pure cache hits.
- ClassicLru must maintain item order, and is internally splicing the fetched item to the head of the linked list.
- MemoryCache and ConcurrentDictionary represent a pure lookup. This is the best case scenario for MemoryCache, since the lookup key is a string (if the key were a Guid, using MemoryCache adds string conversion overhead). 

FastConcurrentLru does not allocate and is approximately 10x faster than MemoryCache.

|                       Method |      Mean |    Error |   StdDev | Ratio |  Gen 0 | Allocated |
|----------------------------- |----------:|---------:|---------:|------:|-------:|----------:|
| ConcurrentDictionaryGetOrAdd |  18.72 ns | 0.289 ns | 0.641 ns |  1.00 |      - |         - |
|    FastConcurrentLruGetOrAdd |  25.64 ns | 0.434 ns | 0.427 ns |  1.35 |      - |         - |
|        ConcurrentLruGetOrAdd |  35.53 ns | 0.259 ns | 0.216 ns |  1.86 |      - |         - |
|   FastConcurrentTLruGetOrAdd | 132.75 ns | 1.493 ns | 1.397 ns |  6.96 |      - |         - |
|       ConcurrentTLruGetOrAdd | 144.87 ns | 2.179 ns | 1.819 ns |  7.59 |      - |         - |
|           ClassicLruGetOrAdd |  75.67 ns | 1.513 ns | 1.554 ns |  3.99 |      - |         - |
|      MemoryCacheGetStringKey | 309.14 ns | 2.155 ns | 1.910 ns | 16.17 | 0.0153 |      32 B |

### Mixed workload

Tests 4 operations, 1 miss (adding the item), 2 hits then remove.

This test needs to be improved to provoke queue cycling.


|               Method |       Mean |    Error |   StdDev | Ratio |  Gen 0 | Allocated |
|--------------------- |-----------:|---------:|---------:|------:|-------:|----------:|
| ConcurrentDictionary |   178.1 ns |  1.47 ns |  1.23 ns |  1.00 | 0.0381 |      80 B |
|    FastConcurrentLru |   420.4 ns |  7.52 ns |  6.67 ns |  2.36 | 0.0534 |     112 B |
|        ConcurrentLru |   423.7 ns |  3.17 ns |  2.64 ns |  2.38 | 0.0534 |     112 B |
|   FastConcurrentTlru |   941.6 ns |  6.69 ns |  5.93 ns |  5.29 | 0.0572 |     120 B |
|       ConcurrentTlru |   960.3 ns | 17.73 ns | 14.80 ns |  5.39 | 0.0572 |     120 B |
|           ClassicLru |   363.5 ns |  3.65 ns |  3.23 ns |  2.04 | 0.0763 |     160 B |
|          MemoryCache | 2,380.9 ns | 33.22 ns | 27.74 ns | 13.37 | 2.3460 |    4912 B |


### LruCycle2

|               Method |       Mean |    Error |   StdDev | Ratio |  Gen 0 | Allocated |
|--------------------- |-----------:|---------:|---------:|------:|-------:|----------:|
| ConcurrentDictionary |   111.0 ns |  1.60 ns |  1.33 ns |  1.00 | 0.0079 |      17 B |
|    FastConcurrentLru | 1,086.2 ns | 21.61 ns | 19.16 ns |  9.77 | 0.1424 |     300 B |
|        ConcurrentLru | 1,098.2 ns |  8.15 ns |  7.23 ns |  9.89 | 0.1424 |     300 B |
|   FastConcurrentTLru | 2,370.7 ns | 33.77 ns | 28.20 ns | 21.37 | 0.1577 |     333 B |
|       ConcurrentTLru | 2,419.7 ns | 46.90 ns | 52.13 ns | 21.82 | 0.1577 |     333 B |
|           ClassicLru |   834.3 ns | 10.84 ns |  9.61 ns |  7.52 | 0.2225 |     467 B |
|          MemoryCache | 1,572.9 ns | 30.94 ns | 44.37 ns | 14.14 | 0.1424 |     313 B |

## Meta-programming using structs for JIT dead code removal and inlining

TemplateConcurrentLru features injectable policies defined as structs. Since structs are subject to special JIT optimizations, the implementation is much faster than if these policies were defined as classes. Using this technique, 'Fast' versions without hit counting are within 30% of the speed of a ConcurrentDictionary.

Since DateTime.UtcNow is around 4x slower than a ConcurrentDictionary lookup, policies that involve time based expiry are significantly slower. Since these are injected as structs and the slow code is optimized away, it is possible maintain the fastest possible speed for the non-TTL policy.
