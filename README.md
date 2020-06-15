# ⚡ BitFaster.Caching

High performance, thread-safe in-memory caching primitives for .NET.

LRU implementations are intended as an alternative to the System.Runtime.Caching.MemoryCache family of classes (e.g. HttpRuntime.Cache, System.Web.Caching et. al.). MemoryCache makes heap allocations when the native object key is not type string, and does not offer the fastest possible performance.

[![NuGet version](https://badge.fury.io/nu/BitFaster.Caching.svg)](https://badge.fury.io/nu/BitFaster.Caching)

# Installing via NuGet
`Install-Package BitFaster.Caching`

# Caching primitives

| Class |  Description |
|:-------|:---------|
| ClassicLru       | Bounded size LRU based with strict ordering.<br><br>Use if ordering is important, but data structures are synchronized with a lock which limits scalability. |
| ConcurrentLru       |  Bounded size pseudo LRU.<br><br>For when you   want a ConcurrentDictionary, but with bounded size. Maintains psuedo order, but is faster than ClassicLru and not prone to lock contention. |
| ConcurrentTlru        | Bounded size pseudo LRU, items have TTL.<br><br>Same as ConcurrentLru, but with a [time aware least recently used (TLRU)](https://en.wikipedia.org/wiki/Cache_replacement_policies#Time_aware_least_recently_used_(TLRU)) eviction policy. |
| FastConcurrentLru/FastConcurrentTLru      | Same as ConcurrentLru/ConcurrentTLru, but with hit counting logic eliminated making them  10-30% faster.   |
| SingletonCache      | Cache singletons by key. Discard when no longer in use. <br><br> For example, cache a SemaphoreSlim per user, where user population is large, but active user count is low.   |
| Scoped<IDisposable>      | A threadsafe wrapper for storing IDisposable objects in a cache that may dispose and invalidate them. The scope keeps the object alive until all callers have finished.   |

# Usage

## Caching IDisposable objects

All cache classes in BitFaster.Caching own the lifetime of cached values, and will automatically dispose values when they are evicted. 

To avoid races using objects after they have been disposed by the cache, wrap them with `Scoped`. The call to `CreateLifetime` creates a `Lifetime` that guarantees the scoped object will not be disposed until the lifetime is disposed. `Scoped` is thread safe, and lifetimes are valid for concurrent callers. 

```csharp
var lru = new ConcurrentLru<int, Scoped<SomeDisposable>>(2, 9, EqualityComparer<int>.Default);
var valueFactory = new SomeDisposableValueFactory();

using (var lifetime = lru.GetOrAdd(1, valueFactory.Create).CreateLifetime())
{
    // lifetime.Value is guaranteed to be alive until the lifetime is disposed
}
```

## Caching Singletons by key

`SingletonCache` enables mapping every key to a single instance of a value, and keeping the value alive only while it is in use. This is useful when the total number of keys is large, but few will be in use at any moment.

The example below shows how to implement exclusive Url access using a lock object per Url. 

```csharp

var urlLocks = new SingletonCache<Url, object>();

Url url = new Url("https://foo.com");

using (var handle = urlLocks.Acquire(url))
{
   lock (handle.Value)
   {
      // exclusive url access
   }
}

```


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

### Lookup keys with a Zipf distribution

Take 1000 samples of a [Zipfan distribution](https://en.wikipedia.org/wiki/Zipf%27s_law) over a set of keys of size *N* and use the keys to lookup values in the cache. If there are $N$ items, the probability of accessing an item numbered *i* or less is (*i* / *N*)^*s*. 

*s* = 0.86 (yields approx 80/20 distribution)<br>
*N* = 500

Cache size = *N* / 10 (so we can cache 10% of the total set). ConcurrentLru has approximately the same performance as ClassicLru in this single threaded test.


|             Method |     Mean |   Error |  StdDev | Ratio | RatioSD |
|------------------- |---------:|--------:|--------:|------:|--------:|
|         ClassicLru | 176.1 ns | 2.74 ns | 2.56 ns |  1.00 |    0.00 |
|  FastConcurrentLru | 178.0 ns | 2.76 ns | 2.45 ns |  1.01 |    0.02 |
|      ConcurrentLru | 185.2 ns | 1.87 ns | 1.56 ns |  1.06 |    0.01 |
| FastConcurrentTLru | 435.7 ns | 2.88 ns | 2.41 ns |  2.48 |    0.03 |
|     ConcurrentTLru | 425.1 ns | 8.46 ns | 7.91 ns |  2.41 |    0.07 |

### Raw Lookup speed

In this test the same items are fetched repeatedly, no items are evicted. Representative of high hit rate scenario, when there are a low number of hot items.

- ConcurrentLru family does not move items in the queues, it is just marking as accessed for pure cache hits.
- ClassicLru must maintain item order, and is internally splicing the fetched item to the head of the linked list.
- MemoryCache and ConcurrentDictionary represent a pure lookup. This is the best case scenario for MemoryCache, since the lookup key is a string (if the key were a Guid, using MemoryCache adds string conversion overhead). 

FastConcurrentLru does not allocate and is approximately 10x faster than MemoryCache.

|               Method |      Mean |    Error |   StdDev | Ratio |  Gen 0 | Allocated |
|--------------------- |----------:|---------:|---------:|------:|-------:|----------:|
| ConcurrentDictionary |  15.83 ns | 0.242 ns | 0.215 ns |  1.00 |      - |         - |
|    FastConcurrentLru |  20.42 ns | 0.319 ns | 0.283 ns |  1.29 |      - |         - |
|        ConcurrentLru |  24.59 ns | 0.484 ns | 0.594 ns |  1.56 |      - |         - |
|   FastConcurrentTLru | 110.76 ns | 0.664 ns | 0.518 ns |  6.98 |      - |         - |
|       ConcurrentTLru | 114.99 ns | 1.652 ns | 1.465 ns |  7.27 |      - |         - |
|           ClassicLru |  69.01 ns | 0.503 ns | 0.446 ns |  4.36 |      - |         - |
|          MemoryCache | 257.83 ns | 4.786 ns | 4.700 ns | 16.30 | 0.0153 |      32 B |

### Mixed workload

Tests 4 operations, 1 miss (adding the item), 2 hits then remove.

This test needs to be improved to provoke queue cycling.

|               Method |       Mean |    Error |   StdDev | Ratio |  Gen 0 | Allocated |
|--------------------- |-----------:|---------:|---------:|------:|-------:|----------:|
| ConcurrentDictionary |   151.7 ns |  2.34 ns |  1.96 ns |  1.00 | 0.0381 |      80 B |
|    FastConcurrentLru |   369.2 ns |  7.29 ns |  7.16 ns |  2.44 | 0.0534 |     112 B |
|        ConcurrentLru |   373.6 ns |  2.97 ns |  2.64 ns |  2.46 | 0.0534 |     112 B |
|   FastConcurrentTlru |   838.6 ns | 11.49 ns | 13.68 ns |  5.53 | 0.0572 |     120 B |
|       ConcurrentTlru |   852.7 ns | 16.12 ns | 13.46 ns |  5.62 | 0.0572 |     120 B |
|           ClassicLru |   347.3 ns |  2.67 ns |  2.08 ns |  2.29 | 0.0763 |     160 B |
|          MemoryCache | 1,987.5 ns | 38.29 ns | 57.31 ns | 13.15 | 2.3460 |    4912 B |


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
