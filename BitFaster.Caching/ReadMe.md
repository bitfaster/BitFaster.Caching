# ⚡ BitFaster.Caching

High performance, thread-safe in-memory caching primitives for .NET.

## ConcurrentLru

`ConcurrentLru` is a light weight drop in replacement for `ConcurrentDictionary`, but with bounded size enforced by the TU-Q pseudo LRU eviction policy. There are no background threads, no lock contention, lookups are fast and hit rate outperforms a pure LRU in all tested scenarios.

Choose a capacity and use just like ConcurrentDictionary, but with bounded size:

```csharp
int capacity = 666;
var lru = new ConcurrentLru<string, SomeItem>(capacity);

var value = lru.GetOrAdd("key", (key) => new SomeItem(key));
```

## ConcurrentLfu

`ConcurrentLfu` is a drop in replacement for `ConcurrentDictionary`, but with bounded size enforced by the [W-TinyLFU eviction policy](https://arxiv.org/pdf/1512.00727.pdf). `ConcurrentLfu` has near optimal hit rate. Reads and writes are buffered and replayed asynchronously to mitigate lock contention.

Choose a capacity and use just like ConcurrentDictionary, but with bounded size:

```csharp
int capacity = 666;
var lfu = new ConcurrentLfu<string, SomeItem>(capacity);

var value = lfu.GetOrAdd("key", (key) => new SomeItem(key));
```
