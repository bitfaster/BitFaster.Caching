# Alternate lookups

Alternate lookups let a cache keyed by one type expose lookup APIs over another key type, provided the configured comparer supports that alternate key. The main use case is a cache with `string` keys that can be queried with `ReadOnlySpan<char>`, avoiding temporary string allocations on the lookup path.

Alternate lookup APIs are available when targeting .NET 9 or later.

## Why use alternate lookups?

If a request arrives as a span, converting it to `string` just to probe the cache adds an allocation on every lookup:

```cs
string key = request.Path[start..end].ToString();
cache.TryGet(key, out var value);
```

With alternate lookups, the cache can use the span directly:

```cs
ReadOnlySpan<char> key = request.Path[start..end];
alternate.TryGet(key, out var value);
```

When the item is already cached, no string needs to be created for the lookup.

## Requirements

- The cache key type must be `string`.
- The cache must be created with a comparer that supports `ReadOnlySpan<char>` as an alternate key.
- The alternate lookup APIs are only available on .NET 9 or later.

For `string` keys, use one of the built-in string comparers that support span-based lookup, for example:

```cs
StringComparer.Ordinal
StringComparer.OrdinalIgnoreCase
```

If the comparer is not compatible, `GetAlternateLookup<TAlternateKey>` and `GetAsyncAlternateLookup<TAlternateKey>` throw `InvalidOperationException`. Use the `TryGet...` forms when compatibility is not guaranteed.

## Basic usage

Create the cache with a compatible comparer, then get an alternate lookup for `ReadOnlySpan<char>`:

```cs
ICache<string, string> cache = new ConcurrentLru<string, string>(1, 1024, StringComparer.Ordinal);

var alternate = cache.GetAlternateLookup<ReadOnlySpan<char>>();

ReadOnlySpan<char> key = "42".AsSpan();

bool found = alternate.TryGet(key, out var value);
```

The same pattern works for other cache implementations that expose `ICache<string, TValue>`, including `ConcurrentLfu` and `ClassicLru`.

## Avoiding allocations on misses

The cache still stores `string` keys. That means a miss that inserts a new item must eventually create the real `string` key. Alternate lookups avoid the temporary allocation used only for probing the cache; they do not remove the need for the stored `string`.

Use `GetOrAdd` so the cache creates the actual key once and passes it to your factory:

```cs
ICache<string, string> cache = new ConcurrentLru<string, string>(1, 1024, StringComparer.Ordinal);
var alternate = cache.GetAlternateLookup<ReadOnlySpan<char>>();

ReadOnlySpan<char> lookupKey = "42".AsSpan();

string value = alternate.GetOrAdd(lookupKey, actualKey => $"value-{actualKey}");
```

On a hit, the cached value is returned with no string allocation for the lookup. On a miss, the comparer materializes the stored `string` key and the factory receives that actual cache key.

## Async and scoped caches

The alternate lookup shape matches the cache type:

- `ICache<string, TValue>` → `GetAlternateLookup<ReadOnlySpan<char>>()`
- `IAsyncCache<string, TValue>` → `GetAsyncAlternateLookup<ReadOnlySpan<char>>()`
- `IScopedCache<string, TValue>` → `GetAlternateLookup<ReadOnlySpan<char>>()`
- `IScopedAsyncCache<string, TValue>` → `GetAsyncAlternateLookup<ReadOnlySpan<char>>()`

For example, async caches use the same idea but with async value creation:

```cs
IAsyncCache<string, string> cache = new ConcurrentLru<string, string>(1, 1024, StringComparer.Ordinal);

var alternate = cache.GetAsyncAlternateLookup<ReadOnlySpan<char>>();
ReadOnlySpan<char> key = "42".AsSpan();

string value = await alternate.GetOrAddAsync(key, actualKey => LoadAsync(actualKey));
```

Scoped caches also expose alternate lookup APIs, but return `Lifetime<T>` in the same way as their regular scoped APIs.

## Compatible and incompatible comparers

When the comparer is known to be compatible, call `GetAlternateLookup<ReadOnlySpan<char>>()` directly.

When the comparer may vary, use `TryGetAlternateLookup<ReadOnlySpan<char>>()`:

```cs
if (cache.TryGetAlternateLookup<ReadOnlySpan<char>>(out var alternate))
{
    return alternate.TryGet(key, out var value);
}

return cache.TryGet(key.ToString(), out value);
```

The same guidance applies to `TryGetAsyncAlternateLookup`.

## Supported operations

Alternate lookups expose the same core operations as the cache:

- `TryGet`
- `TryRemove`
- `TryUpdate`
- `AddOrUpdate`
- `GetOrAdd` / `GetOrAddAsync`

For scoped caches the equivalent operations are `ScopedTryGet` and `ScopedGetOrAdd` / `ScopedGetOrAddAsync`.

## Summary

Use alternate lookups when:

- the cache is keyed by `string`
- the incoming lookup key is already available as `ReadOnlySpan<char>`
- the cache comparer supports span-based alternate keys

This keeps the hot lookup path allocation-free on cache hits while preserving the existing `string` key API and semantics.
