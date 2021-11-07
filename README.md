# ⚡ BitFaster.Caching

High performance, thread-safe in-memory caching primitives for .NET.

[![NuGet version](https://badge.fury.io/nu/BitFaster.Caching.svg)](https://badge.fury.io/nu/BitFaster.Caching) ![Nuget](https://img.shields.io/nuget/dt/bitfaster.caching) ![.NET Core](https://github.com/bitfaster/BitFaster.Caching/workflows/.NET%20Core/badge.svg)

# Installing via NuGet
`Install-Package BitFaster.Caching`

# Overview

| Class |  Description |
|:-------|:---------|
| [ConcurrentLru](https://github.com/bitfaster/BitFaster.Caching/wiki/ConcurrentLru)       |  Represents a thread-safe bounded size pseudo LRU.<br><br>A drop in replacement for ConcurrentDictionary, but with bounded size. Maintains psuedo order, with better hit rate than a pure Lru and not prone to lock contention. |
| [ConcurrentTLru](https://github.com/bitfaster/BitFaster.Caching/wiki/ConcurrentTLru)        | Represents a thread-safe bounded size pseudo TLRU, items have TTL.<br><br>As ConcurrentLru, but with a [time aware least recently used (TLRU)](https://en.wikipedia.org/wiki/Cache_replacement_policies#Time_aware_least_recently_used_(TLRU)) eviction policy. If the values generated for each key can change over time, ConcurrentTLru is eventually consistent where the inconsistency window = TTL. |
| SingletonCache      | Represents a thread-safe cache of key value pairs, which guarantees a single instance of each value. Values are discarded immediately when no longer in use to conserve memory.  |
| Scoped<IDisposable>      | Represents a thread-safe wrapper for storing IDisposable objects in a cache that may dispose and invalidate them. The scope keeps the object alive until all callers have finished.   |

# Quick Start

Please refer to the [wiki](https://github.com/bitfaster/BitFaster.Caching/wiki) for more detailed documentation.
    
## ConcurrentLru/ConcurrentTLru

`ConcurrentLru` and `ConcurrentTLru` are intended as a drop in replacement for `ConcurrentDictionary`, and a much faster alternative to the `System.Runtime.Caching.MemoryCache` family of classes (e.g. `HttpRuntime.Cache`, `System.Web.Caching` etc). 

Choose a capacity and use just like ConcurrentDictionary:

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

MemoryCache is perfectly servicable, but it has some limitations:

- Makes heap allocations when the native object key is not type string.
- Is not 'scan' resistant, fetching all keys will load everything into memory.
- Does not scale well with concurrent writes.
- Contains perf counters that can't be disabled
- Uses an heuristic to estimate memory used, and evicts items using a timer. The 'trim' process may remove useful items, and if the timer does not fire fast enough the resulting memory pressure can be problematic (e.g. induced GC).

# Performance

*DISCLAIMER: Always measure performance in the context of your application. The results provided here are intended as a guide.*
    
The cache replacement policy must maximize the cache hit rate, and minimize the computational and space overhead involved in implementing the policy. Below an analysis of hit rate vs cache size, latency and throughput is provided.  

## ConcurrentLru Hit rate

The charts below show the relative hit rate of classic LRU vs Concurrent LRU on a [Zipfian distribution](https://en.wikipedia.org/wiki/Zipf%27s_law) of input keys, with parameter *s* = 0.5 and *s* = 0.86 respectively. If there are *N* items, the probability of accessing an item numbered *i* or less is (*i* / *N*)^*s*. 

Here *N* = 50000, and we take 1 million sample keys. The hit rate is the number of times we get a cache hit divided by 1 million.
This test was repeated with the cache configured to different sizes expressed as a percentage *N* (e.g. 10% would be a cache with a capacity 5000).

<table>
  <tr>
    <td>
<img src="https://user-images.githubusercontent.com/12851828/84844130-a00d4680-affe-11ea-8f7a-e3c66180d8b9.png" width="350"/>
</td>
    <td>
<img src="https://user-images.githubusercontent.com/12851828/84844172-b6b39d80-affe-11ea-9a29-cbdae6020246.png" width="350"/>
</td>
   </tr> 
</table>

As above, but interleaving a sequential scan of every key (aka sequential flooding). In this case, ConcurrentLru performs better across the board, and is more resistant to scanning.

<table>
  <tr>
    <td>
<img src="https://user-images.githubusercontent.com/12851828/84841922-a4366580-aff8-11ea-93dd-568d60cd82d9.png" width="350"/>
</td>
    <td>
<img src="https://user-images.githubusercontent.com/12851828/84842237-730a6500-aff9-11ea-9a46-40141adff920.png" width="350"/>
</td>
   </tr> 
</table>

These charts summarize the percentage increase in hit rate ConcurrentLru vs LRU. Increase is in hit rate is significant at lower cache sizes.

<table>
  <tr>
    <td>
<img src="https://user-images.githubusercontent.com/12851828/84843966-283f1c00-affe-11ea-99c9-20aa01f307f0.png" width="350"/>
</td>
    <td>
<img src="https://user-images.githubusercontent.com/12851828/84844003-3d1baf80-affe-11ea-9266-e83efe2e8c35.png" width="350"/>
</td>
   </tr> 
</table>

## ConcurrentLru Latency

In these benchmarks, a cache miss is essentially free. These tests exist purely to compare the raw execution speed of the cache bookkeeping code. In a real setting, where a cache miss is presumably quite expensive, the relative overhead of the cache will be very small.

Benchmarks are based on BenchmarkDotNet, so are single threaded. The ConcurrentLru family of classes are composed internally of ConcurrentDictionary.GetOrAdd and ConcurrentQueue.Enqueue/Dequeue method calls, and scale well to concurrent workloads.

Benchmark results below are from a computer with a mobile class Broadwell CPU with small caches (128kb L1/512kb L2/4mb L3):

~~~
BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19041.264 (2004/?/20H1)
Intel Core i7-5600U CPU 2.60GHz (Broadwell), 1 CPU, 4 logical and 2 physical cores
.NET Core SDK=3.1.301
  [Host]    : .NET Core 3.1.5 (CoreCLR 4.700.20.26901, CoreFX 4.700.20.27001), X64 RyuJIT
  RyuJitX64 : .NET Core 3.1.5 (CoreCLR 4.700.20.26901, CoreFX 4.700.20.27001), X64 RyuJIT

Job=RyuJitX64  Jit=RyuJit  Platform=X64
~~~
    
Benchmarks have been repeated across supported .NET Frameworks and on the CPU architectures available in Azure (e.g. Intel Skylake, AMD Zen). Results are repeatable within +/-5%.

### What are FastConcurrentLru/FastConcurrentTLru?

These are classes that execute with the hit counting logic eliminated (via JIT). If hit counts are not required, this makes the code around 10% faster.

### Lookup keys with a Zipf distribution

Take 1000 samples of a [Zipfian distribution](https://en.wikipedia.org/wiki/Zipf%27s_law) over a set of keys of size *N* and use the keys to lookup values in the cache. If there are *N* items, the probability of accessing an item numbered *i* or less is (*i* / *N*)^*s*. 

*s* = 0.86 (yields approx 80/20 distribution)<br>
*N* = 500

Cache size = *N* / 10 (so we can cache 10% of the total set). ConcurrentLru has approximately the same computational overhead as a standard LRU in this single threaded test.

|             Method |     Mean |   Error |  StdDev | Ratio | RatioSD |
|------------------- |---------:|--------:|--------:|------:|--------:|
|         ClassicLru | 175.7 ns | 2.75 ns | 2.43 ns |  1.00 |    0.00 |
|  FastConcurrentLru | 180.2 ns | 2.55 ns | 2.26 ns |  1.03 |    0.02 |
|      ConcurrentLru | 189.1 ns | 3.14 ns | 2.94 ns |  1.08 |    0.03 |
| FastConcurrentTLru | 261.4 ns | 4.53 ns | 4.01 ns |  1.49 |    0.04 |
|     ConcurrentTLru | 266.1 ns | 3.96 ns | 3.51 ns |  1.51 |    0.03 |

### Raw Lookup speed

In this test the same items are fetched repeatedly, no items are evicted. Representative of high hit rate scenario, when there are a low number of hot items.

- ConcurrentLru family does not move items in the queues, it is just marking as accessed for pure cache hits.
- Classic Lru must maintain item order, and is internally splicing the fetched item to the head of the linked list.
- MemoryCache and ConcurrentDictionary represent a pure lookup. This is the best case scenario for MemoryCache, since the lookup key is a string (if the key were a Guid, using MemoryCache adds string conversion overhead). 

FastConcurrentLru does not allocate and is approximately 10x faster than System.Runtime.Caching.MemoryCache or the newer Microsoft.Extensions.Caching.Memory.MemoryCache.

|                   Method |      Mean |    Error |   StdDev | Ratio |  Gen 0 | Allocated |
|------------------------- |----------:|---------:|---------:|------:|-------:|----------:|
|     ConcurrentDictionary |  16.76 ns | 0.322 ns | 0.285 ns |  1.00 |      - |         - |
|        FastConcurrentLru |  18.94 ns | 0.249 ns | 0.220 ns |  1.13 |      - |         - |
|            ConcurrentLru |  21.46 ns | 0.204 ns | 0.191 ns |  1.28 |      - |         - |
|       FastConcurrentTLru |  41.57 ns | 0.450 ns | 0.376 ns |  2.48 |      - |         - |
|           ConcurrentTLru |  43.95 ns | 0.588 ns | 0.521 ns |  2.62 |      - |         - |
|               ClassicLru |  67.62 ns | 0.901 ns | 0.799 ns |  4.03 |      - |         - |
|    RuntimeMemoryCacheGet | 279.70 ns | 3.825 ns | 3.578 ns | 16.70 | 0.0153 |      32 B |
| ExtensionsMemoryCacheGet | 341.67 ns | 6.617 ns | 6.499 ns | 20.35 | 0.0114 |      24 B |


## ConcurrentLru Throughput

In this test, we generate 2000 samples of 500 keys with a Zipfian distribution (s = 0.86). Caches have size 50. From N concurrent threads, fetch the sample keys in sequence (each thread is using the same input keys). The principal scalability limit in concurrent applications is the exclusive resource lock. As the number of threads increases, ConcurrentLru significantly outperforms an LRU implemented with a short lived exclusive lock used to synchronize the linked list data structure.

This test was run on a Standard D16s v3 Azure VM (16 cpus), with .NET Core 3.1.

![image](https://user-images.githubusercontent.com/12851828/86203563-2f941880-bb1a-11ea-8d6a-70ece91b4362.png)
