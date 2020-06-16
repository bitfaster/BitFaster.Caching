# ⚡ BitFaster.Caching

High performance, thread-safe in-memory caching primitives for .NET.

[![NuGet version](https://badge.fury.io/nu/BitFaster.Caching.svg)](https://badge.fury.io/nu/BitFaster.Caching)

# Installing via NuGet
`Install-Package BitFaster.Caching`

# API

| Class |  Description |
|:-------|:---------|
| ConcurrentLru       |  Bounded size pseudo LRU.<br><br>A drop in replacement for ConcurrentDictionary, but with bounded size. Maintains psuedo order, with better hit rate than a pure Lru and not prone to lock contention. |
| ConcurrentTlru        | Bounded size pseudo LRU, items have TTL.<br><br>Same as ConcurrentLru, but with a [time aware least recently used (TLRU)](https://en.wikipedia.org/wiki/Cache_replacement_policies#Time_aware_least_recently_used_(TLRU)) eviction policy. If the values generated for each key can change over time, ConcurrentTlru is eventually consistent where the inconsistency window = TTL. |
| SingletonCache      | Cache singleton objects by key. Discard when no longer in use. Threadsafe guarantee of single instance.  |
| Scoped<IDisposable>      | A threadsafe wrapper for storing IDisposable objects in a cache that may dispose and invalidate them. The scope keeps the object alive until all callers have finished.   |

# Usage

## ConcurrentLru/ConcurrentTLru

`ConcurrentLru` and `ConcurrentTLru` are intended as a drop in replacement for `ConcurrentDictionary`, and a much faster alternative to the `System.Runtime.Caching.MemoryCache` family of classes (e.g. `HttpRuntime.Cache`, `System.Web.Caching` etc). 

```csharp
int capacity = 666;
var lru = new ConcurrentLru<int, SomeItem>(capacity);

var value = lru.GetOrAdd(1, (k) => new SomeItem(k));
var value = await lru.GetOrAddAsync(0, (k) => Task.FromResult(new SomeItem(k)));
```


## Caching IDisposable objects

All cache classes in BitFaster.Caching own the lifetime of cached values, and will automatically dispose values when they are evicted. 

To avoid races using objects after they have been disposed by the cache, wrap them with `Scoped`. The call to `CreateLifetime` creates a `Lifetime` that guarantees the scoped object will not be disposed until the lifetime is disposed. `Scoped` is thread safe, and guarantees correct disposal for concurrent lifetimes. 

```csharp
int capacity = 666;
var lru = new ConcurrentLru<int, Scoped<SomeDisposable>>(capacity);
var valueFactory = new SomeDisposableValueFactory();

using (var lifetime = lru.GetOrAdd(1, valueFactory.Create).CreateLifetime())
{
    // lifetime.Value is guaranteed to be alive until the lifetime is disposed
}

class SomeDisposableValueFactory
{
   public Scoped<SomeDisposable>> Create(int key)
   {
      return new Scoped<SomeDisposable>(new SomeDisposable(key));
   }
}
```

## Caching Singletons by key

`SingletonCache` enables mapping every key to a single instance of a value, and keeping the value alive only while it is in use. This is useful when the total number of keys is large, but few will be in use at any moment.

The example below shows how to implement exclusive Url access using a lock object per Url. 

```csharp

var urlLocks = new SingletonCache<Url, object>();

Url url = new Url("https://foo.com");

using (var lifetime = urlLocks.Acquire(url))
{
   lock (lifetime.Value)
   {
      // exclusive url access
   }
}

```


### Why not use MemoryCache?

MemoryCache is perfectly servicable. But in some situations, it can be a bottleneck.

- Makes heap allocations when the native object key is not type string.
- Does not scale well with concurrent writes.
- Executes code for perf counters that can't be disabled
- Uses an heuristic to estimate memory used, and the 'trim' process may remove useful items. If many items are added quickly, runaway is a problem.

# Performance

## ConcurrentLru Hit rate

The charts below show the relative hit rate of classic LRU vs Concurrent LRU on a [Zipfian distribution](https://en.wikipedia.org/wiki/Zipf%27s_law) of input keys, with parameter *s* = 0.5 and *s* = 0.86 respectively. If there are *N* items, the probability of accessing an item numbered *i* or less is (*i* / *N*)^*s*. 

Here *N* = 50000, and we take 1 million sample keys. The hit rate is the number of times we get a cache hit divided by 1 million.
This test was repeated with the cache configured to different sizes expressed as a percentage *N* (e.g. 10% would be a cache with a capacity 5000).

When the cache is small, below 15% of the total key space, ConcurrentLru outperforms Lru. In the best case, for *s*=0.5, when the cache is 2.5% of the total key space ConcurrentLru outperforms LRU by more than 50%.

<table>
  <tr>
    <td>
<img src="https://user-images.githubusercontent.com/12851828/84707621-e2a62480-af13-11ea-91e7-726911bce162.png" width="400"/>
</td>
    <td>
<img src="https://user-images.githubusercontent.com/12851828/84707663-f81b4e80-af13-11ea-96d4-1ba71444d333.png" width="400"/>
</td>
   </tr> 
</table>

## ConcurrentLru Benchmarks

In the benchmarks, a cache miss is essentially free. These tests exist purely to compare the raw execution speed of the cache code. In a real setting, where a cache miss is presumably quite expensive, the relative overhead of the cache will be very small.

Benchmarks are based on BenchmarkDotNet, so are single threaded. The ConcurrentLru family of classes can outperform ClassicLru in multithreaded workloads.

All benchmarks below are run on this measly laptop:

~~~
BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19041.264 (2004/?/20H1)
Intel Core i7-5600U CPU 2.60GHz (Broadwell), 1 CPU, 4 logical and 2 physical cores
.NET Core SDK=3.1.301
  [Host]    : .NET Core 3.1.5 (CoreCLR 4.700.20.26901, CoreFX 4.700.20.27001), X64 RyuJIT
  RyuJitX64 : .NET Core 3.1.5 (CoreCLR 4.700.20.26901, CoreFX 4.700.20.27001), X64 RyuJIT

Job=RyuJitX64  Jit=RyuJit  Platform=X64
~~~

### Lookup keys with a Zipf distribution

Take 1000 samples of a [Zipfian distribution](https://en.wikipedia.org/wiki/Zipf%27s_law) over a set of keys of size *N* and use the keys to lookup values in the cache. If there are *N* items, the probability of accessing an item numbered *i* or less is (*i* / *N*)^*s*. 

*s* = 0.86 (yields approx 80/20 distribution)<br>
*N* = 500

Cache size = *N* / 10 (so we can cache 10% of the total set). ConcurrentLru has approximately the same performance as a standard Lru in this single threaded test.

|             Method |     Mean |   Error |  StdDev | Ratio | RatioSD |
|------------------- |---------:|--------:|--------:|------:|--------:|
|         ClassicLru | 157.3 ns | 1.67 ns | 1.48 ns |  1.00 |    0.00 |
|  FastConcurrentLru | 165.4 ns | 1.17 ns | 1.04 ns |  1.05 |    0.01 |
|      ConcurrentLru | 176.1 ns | 1.22 ns | 1.08 ns |  1.12 |    0.01 |
| FastConcurrentTLru | 247.9 ns | 3.58 ns | 2.80 ns |  1.58 |    0.02 |
|     ConcurrentTLru | 259.0 ns | 3.61 ns | 3.20 ns |  1.65 |    0.03 |

### Raw Lookup speed

In this test the same items are fetched repeatedly, no items are evicted. Representative of high hit rate scenario, when there are a low number of hot items.

- ConcurrentLru family does not move items in the queues, it is just marking as accessed for pure cache hits.
- ClassicLru must maintain item order, and is internally splicing the fetched item to the head of the linked list.
- MemoryCache and ConcurrentDictionary represent a pure lookup. This is the best case scenario for MemoryCache, since the lookup key is a string (if the key were a Guid, using MemoryCache adds string conversion overhead). 

FastConcurrentLru does not allocate and is approximately 10x faster than MemoryCache.

|               Method |      Mean |    Error |   StdDev | Ratio |  Gen 0 | Allocated |
|--------------------- |----------:|---------:|---------:|------:|-------:|----------:|
| ConcurrentDictionary |  15.06 ns | 0.286 ns | 0.307 ns |  1.00 |      - |         - |
|    FastConcurrentLru |  20.70 ns | 0.276 ns | 0.258 ns |  1.37 |      - |         - |
|        ConcurrentLru |  24.09 ns | 0.270 ns | 0.253 ns |  1.60 |      - |         - |
|   FastConcurrentTLru |  49.57 ns | 0.619 ns | 0.517 ns |  3.30 |      - |         - |
|       ConcurrentTLru |  64.82 ns | 2.547 ns | 7.391 ns |  4.50 |      - |         - |
|           ClassicLru |  76.78 ns | 1.412 ns | 3.039 ns |  5.25 |      - |         - |
|          MemoryCache | 278.37 ns | 3.887 ns | 3.035 ns | 18.50 | 0.0153 |      32 B |


## Meta-programming using structs for JIT dead code removal and inlining

TemplateConcurrentLru features injectable policies defined as structs. Since structs are subject to special JIT optimizations, the implementation is much faster than if these policies were defined as classes. Using this technique, 'Fast' versions without hit counting are within 30% of the speed of a ConcurrentDictionary.

Since DateTime.UtcNow is around 4x slower than a ConcurrentDictionary lookup, policies that involve time based expiry are significantly slower. Since these are injected as structs and the slow code is optimized away, it is possible maintain the fastest possible speed for the non-TTL policy.
