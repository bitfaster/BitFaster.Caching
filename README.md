# ⚡ BitFaster.Caching

High performance, thread-safe in-memory caching primitives for .NET.

[![NuGet version](https://badge.fury.io/nu/BitFaster.Caching.svg)](https://badge.fury.io/nu/BitFaster.Caching) ![Nuget](https://img.shields.io/nuget/dt/bitfaster.caching) ![.NET Core](https://github.com/bitfaster/BitFaster.Caching/workflows/.NET%20Core/badge.svg) [![Coverage Status](https://coveralls.io/repos/github/bitfaster/BitFaster.Caching/badge.svg?branch=main)](https://coveralls.io/github/bitfaster/BitFaster.Caching?branch=main)

BitFaster.Caching uses modern techniques to optimize hit rate, concurrent throughput and latency.

# Features

- [ConcurrentLru](https://github.com/bitfaster/BitFaster.Caching/wiki/ConcurrentLru), a lightweight pseudo LRU based on the [2Q](https://www.vldb.org/conf/1994/P439.PDF)) eviction policy. Also with [time based eviction](https://github.com/bitfaster/BitFaster.Caching/wiki/ConcurrentTLru).
- [ConcurrentLfu](https://github.com/bitfaster/BitFaster.Caching/wiki/ConcurrentLfu), an approximate LFU based on the [W-TinyLFU](https://arxiv.org/pdf/1512.00727.pdf) eviction policy.
- Configurable [atomic valueFactory](https://github.com/bitfaster/BitFaster.Caching/wiki/Atomic-GetOrAdd) to mitigate [cache stampede](https://en.wikipedia.org/wiki/Cache_stampede).
- Configurable [thread-safe wrappers for IDisposable](https://github.com/bitfaster/BitFaster.Caching/wiki/IDisposable-and-Scoped-values) cache values.
- A [builder API](https://github.com/bitfaster/BitFaster.Caching/wiki/Cache-Builders) to easily configure cache features.
- [SingletonCache](https://github.com/bitfaster/BitFaster.Caching/wiki/SingletonCache) for caching single instance values, such as lock objects.
- High performance [concurrent counters](https://github.com/bitfaster/BitFaster.Caching/wiki/Metrics).

# Documentation

Please refer to the [wiki](https://github.com/bitfaster/BitFaster.Caching/wiki) for full API documentation, and a complete analysis of hit rate, latency and throughput.

# Getting started
    
BitFaster.Caching is installed from NuGet:

`dotnet add package BitFaster.Caching`

## ConcurrentLru

`ConcurrentLru` is a light weight drop in replacement for `ConcurrentDictionary`, but with bounded size enforced by the TU-Q eviction policy (similar to [2Q](https://www.vldb.org/conf/1994/P439.PDF)). There are no background threads, no global locks, concurrent throughput is high, lookups are fast and hit rate outperforms a pure LRU in all tested scenarios.

Choose a capacity and use just like `ConcurrentDictionary`, but with bounded size:

```csharp
int capacity = 666;
var lru = new ConcurrentLru<string, SomeItem>(capacity);

var value = lru.GetOrAdd("key", (key) => new SomeItem(key));
```

## ConcurrentLfu

`ConcurrentLfu` is a drop in replacement for `ConcurrentDictionary`, but with bounded size enforced by the [W-TinyLFU eviction policy](https://arxiv.org/pdf/1512.00727.pdf). `ConcurrentLfu` has near optimal hit rate and high scalability. Reads and writes are buffered then replayed asynchronously to mitigate lock contention.

Choose a capacity and use just like `ConcurrentDictionary`, but with bounded size:

```csharp
int capacity = 666;
var lfu = new ConcurrentLfu<string, SomeItem>(capacity);

var value = lfu.GetOrAdd("key", (key) => new SomeItem(key));
```
