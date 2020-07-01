# ⚡ BitFaster.Caching

High performance, thread-safe in-memory caching primitives for .NET.

[![NuGet version](https://badge.fury.io/nu/BitFaster.Caching.svg)](https://badge.fury.io/nu/BitFaster.Caching) ![.NET Core](https://github.com/bitfaster/BitFaster.Caching/workflows/.NET%20Core/badge.svg)

# Installing via NuGet
`Install-Package BitFaster.Caching`

# Overview

| Class |  Description |
|:-------|:---------|
| ConcurrentLru       |  Represents a thread-safe bounded size pseudo LRU.<br><br>A drop in replacement for ConcurrentDictionary, but with bounded size. Maintains psuedo order, with better hit rate than a pure Lru and not prone to lock contention. |
| ConcurrentTLru        | Represents a thread-safe bounded size pseudo TLRU, items have TTL.<br><br>As ConcurrentLru, but with a [time aware least recently used (TLRU)](https://en.wikipedia.org/wiki/Cache_replacement_policies#Time_aware_least_recently_used_(TLRU)) eviction policy. If the values generated for each key can change over time, ConcurrentTLru is eventually consistent where the inconsistency window = TTL. |
| SingletonCache      | Represents a thread-safe cache of key value pairs, which guarantees a single instance of each value. Values are discarded immediately when no longer in use to conserve memory.  |
| Scoped<IDisposable>      | Represents a thread-safe wrapper for storing IDisposable objects in a cache that may dispose and invalidate them. The scope keeps the object alive until all callers have finished.   |

# Usage

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

The cache replacement policy must maximize the cache hit rate, and minimize the computational and space overhead involved in implementing the policy. Below an analysis of both the hit rate vs cache size, and run time overhead is provided.  

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

All benchmarks below are run on the same computer:

~~~
BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19041.264 (2004/?/20H1)
Intel Core i7-5600U CPU 2.60GHz (Broadwell), 1 CPU, 4 logical and 2 physical cores
.NET Core SDK=3.1.301
  [Host]    : .NET Core 3.1.5 (CoreCLR 4.700.20.26901, CoreFX 4.700.20.27001), X64 RyuJIT
  RyuJitX64 : .NET Core 3.1.5 (CoreCLR 4.700.20.26901, CoreFX 4.700.20.27001), X64 RyuJIT

Job=RyuJitX64  Jit=RyuJit  Platform=X64
~~~

### What are FastConcurrentLru/FastConcurrentTLru?

These are classes that execute with the hit counting logic eliminated (via JIT). If hit counts are not required, this makes the code around 10% faster.

### Lookup keys with a Zipf distribution

Take 1000 samples of a [Zipfian distribution](https://en.wikipedia.org/wiki/Zipf%27s_law) over a set of keys of size *N* and use the keys to lookup values in the cache. If there are *N* items, the probability of accessing an item numbered *i* or less is (*i* / *N*)^*s*. 

*s* = 0.86 (yields approx 80/20 distribution)<br>
*N* = 500

Cache size = *N* / 10 (so we can cache 10% of the total set). ConcurrentLru has approximately the same computational overhead as a standard LRU in this single threaded test.

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
- Classic Lru must maintain item order, and is internally splicing the fetched item to the head of the linked list.
- MemoryCache and ConcurrentDictionary represent a pure lookup. This is the best case scenario for MemoryCache, since the lookup key is a string (if the key were a Guid, using MemoryCache adds string conversion overhead). 

FastConcurrentLru does not allocate and is approximately 10x faster than System.Runtime.Caching.MemoryCache or the newer Microsoft.Extensions.Caching.Memory.MemoryCache.

|                Method |      Mean |    Error |   StdDev | Ratio |  Gen 0 | Allocated |
|---------------------- |----------:|---------:|---------:|------:|-------:|----------:|
|  ConcurrentDictionary |  16.88 ns | 0.276 ns | 0.245 ns |  1.00 |      - |         - |
|     FastConcurrentLru |  23.27 ns | 0.491 ns | 0.565 ns |  1.38 |      - |         - |
|         ConcurrentLru |  26.77 ns | 0.512 ns | 0.666 ns |  1.60 |      - |         - |
|    FastConcurrentTLru |  54.35 ns | 0.650 ns | 0.576 ns |  3.22 |      - |         - |
|        ConcurrentTLru |  60.10 ns | 1.024 ns | 1.501 ns |  3.53 |      - |         - |
|            ClassicLru |  68.04 ns | 1.400 ns | 2.221 ns |  4.12 |      - |         - |
|    RuntimeMemoryCache | 280.16 ns | 5.607 ns | 7.486 ns | 16.59 | 0.0153 |      32 B |
| ExtensionsMemoryCache | 342.72 ns | 3.729 ns | 3.114 ns | 20.29 | 0.0114 |      24 B |


## ConcurrentLru Throughput

In this test, we generate 2000 samples of 500 keys with a Zipfian distribution (s = 0.86). Caches have size 50. From N concurrent threads, fetch the sample keys in sequence (each thread is using the same input keys). The principal scalability limit in concurrent applications is the exclusive resource lock. As the number of threads increases, ConcurrentLru significantly outperforms an LRU implemented with a short lived exclusive lock used to synchronize the linked list data structure.

This test was run on a Standard D16s v3 Azure VM (16 cpus), with .NET Core 3.1.



## Meta-programming using structs and JIT value type optimization

TemplateConcurrentLru features injectable behaviors defined as structs. Structs are subject to special JIT optimizations, and the .NET JIT compiler can inline, eliminate dead code and propogate JIT time constants based on structs. Using this technique, the TemplateConcurrentLru can be customized to support LRU and TLRU policies without compromising execution speed.

### Example: TemplateConcurrentLru.TryGet

This is the source code for the TryGet method. It calls into two value type generic type arguments: policy (1) and hitcounter (2). 

```csharp
public bool TryGet(K key, out V value)
{
    I item;
    if (dictionary.TryGetValue(key, out item))
    {
        if (this.policy.ShouldDiscard(item)) // 1
        {
            this.Move(item, ItemDestination.Remove);
            value = default(V);
            return false;
        }

        value = item.Value;
        this.policy.Touch(item);
        this.hitCounter.IncrementHit(); // 2
        return true;
    }

    value = default(V);
    this.hitCounter.IncrementMiss(); // 2
    return false;
}
```

### FastConcurrentLru (LruPolicy & NullHitCounter)

The LruPolicy used by FastConcurrentLru/ConcurrentLru is hardcoded to never discard items.

```csharp
   public readonly struct LruPolicy<K, V> : IPolicy<K, V, LruItem<K, V>>
   {
        ...
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ShouldDiscard(LruItem<K, V> item)
        {
            return false;
        }
        ...
   }
```

The branch and enclosed code for ShouldDiscard are completely eliminated by JIT (1). Since the below code is from FastConcurrentLru, the hit counting calls (2) are also eliminated. The assembly code for the FastConcurrentLru.TryGet method with LruPolicy and NullHitCounter is 76 bytes:

```assembly
; BitFaster.Caching.Lru.TemplateConcurrentLru`5[[System.Int32, System.Private.CoreLib],[System.Int32, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib],[BitFaster.Caching.Lru.LruPolicy`2[[System.Int32, System.Private.CoreLib],[System.Int32, System.Private.CoreLib]], BitFaster.Caching],[BitFaster.Caching.Lru.NullHitCounter, BitFaster.Caching]].TryGet(Int32, Int32 ByRef)
       push      rsi
       sub       rsp,30
       xor       eax,eax
       mov       [rsp+28],rax
       mov       rsi,r8
       mov       rcx,[rcx+8]
       lea       r8,[rsp+28]
       cmp       [rcx],ecx
       call      qword ptr [7FFED15190A0]
       test      eax,eax
       je        short M01_L00
       mov       rax,[rsp+28]
       mov       eax,[rax+0C]
       mov       [rsi],eax
       mov       rax,[rsp+28]
       mov       byte ptr [rax+10],1
       mov       eax,1
       add       rsp,30
       pop       rsi
       ret
M01_L00:
       xor       eax,eax
       mov       [rsi],eax
       add       rsp,30
       pop       rsi
       ret
; Total bytes of code 76
```

### FastConcurrentTLru (TLruLongTicksPolicy & NullHitCounter)

The struct TLruLongTicksPolicy used in FastConcurrentTLru can expire items.

```csharp
   public readonly struct TLruLongTicksPolicy<K, V> : IPolicy<K, V, LongTickCountLruItem<K, V>>
   {
        ...
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ShouldDiscard(LongTickCountLruItem<K, V> item)
        {
            if (Stopwatch.GetTimestamp() - item.TickCount > this.timeToLive)
            {
                return true;
            }

            return false;
        }
        ...
   }
```

As a result, the JITted code now includes branch 2 and the assembly code grows considerably to 312 bytes.

```assembly
; BitFaster.Caching.Lru.TemplateConcurrentLru`5[[System.Int32, System.Private.CoreLib],[System.Int32, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib],[BitFaster.Caching.Lru.TLruLongTicksPolicy`2[[System.Int32, System.Private.CoreLib],[System.Int32, System.Private.CoreLib]], BitFaster.Caching],[BitFaster.Caching.Lru.NullHitCounter, BitFaster.Caching]].TryGet(Int32, Int32 ByRef)
       push      rbp
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,88
       lea       rbp,[rsp+0C0]
       xor       ebx,ebx
       mov       [rbp+0FFC0],rbx
       mov       [rbp+0FFB8],rbx
       mov       [rbp+20],r8
       mov       rsi,rcx
       mov       ebx,edx
       lea       rcx,[rbp+0FF70]
       mov       rdx,r10
       call      CORINFO_HELP_INIT_PINVOKE_FRAME
       mov       r14,rax
       mov       rcx,rsp
       mov       [rbp+0FF90],rcx
       mov       rcx,rbp
       mov       [rbp+0FFA0],rcx
       mov       rcx,[rsi+8]
       lea       r8,[rbp+0FFC0]
       mov       edx,ebx
       cmp       [rcx],ecx
       call      qword ptr [7FFED15290A0]
       test      eax,eax
       je        near ptr M01_L03
       mov       [rbp+10],rsi
       mov       rbx,[rsi+40]
       mov       r15,[rbp+0FFC0]
       mov       [rbp+0FFB0],r15
       lea       rcx,[rbp+0FFB8]
       xor       r11d,r11d
       mov       rax,offset MD_Interop+Kernel32.QueryPerformanceCounter(Int64*)
       mov       [rbp+0FF80],rax
       lea       rax,[M01_L00]
       mov       [rbp+0FF98],rax
       lea       rax,[rbp+0FF70]
       mov       [r14+10],rax
       mov       byte ptr [r14+0C],0
       call      qword ptr [7FFED151D7D0]
M01_L00:
       mov       byte ptr [r14+0C],1
       cmp       dword ptr [7FFED1524BD8],0
       je        short M01_L01
       call      qword ptr [7FFED1528278]
M01_L01:
       mov       rcx,[rbp+0FF78]
       mov       [r14+10],rcx
       mov       rcx,[rbp+0FFB8]
       mov       r15,[rbp+0FFB0]
       sub       rcx,[r15+18]
       cmp       rcx,rbx
       jle       short M01_L02
       mov       rcx,[rbp+10]
       mov       rdx,[rbp+0FFC0]
       mov       r8d,2
       call      BitFaster.Caching.Lru.TemplateConcurrentLru`5[[System.Int32, System.Private.CoreLib],[System.Int32, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib],[BitFaster.Caching.Lru.TLruLongTicksPolicy`2[[System.Int32, System.Private.CoreLib],[System.Int32, System.Private.CoreLib]], BitFaster.Caching],[BitFaster.Caching.Lru.NullHitCounter, BitFaster.Caching]].Move(System.__Canon, BitFaster.Caching.Lru.ItemDestination)
       xor       eax,eax
       mov       rdi,[rbp+20]
       mov       [rdi],eax
       jmp       short M01_L04
M01_L02:
       mov       rax,[rbp+0FFC0]
       mov       eax,[rax+0C]
       mov       rdi,[rbp+20]
       mov       [rdi],eax
       mov       rax,[rbp+0FFC0]
       mov       byte ptr [rax+10],1
       mov       eax,1
       jmp       short M01_L04
M01_L03:
       xor       eax,eax
       mov       rdi,[rbp+20]
       mov       [rdi],eax
M01_L04:
       movzx     eax,al
       mov       byte ptr [r14+0C],1
       lea       rsp,[rbp+0FFC8]
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
; Total bytes of code 312
```
