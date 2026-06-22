# Plan: Weight-based eviction for ConcurrentLfu (Caffeine parity)

## 1. Goal
Add an **opt-in, weight-based** eviction policy to `ConcurrentLfu`, faithfully matching
Caffeine's weighted W-TinyLFU. A user supplies a weigher `weigh(key, value) -> int (>= 0)`;
the cache bounds the **total weight** (not item count). The default/unweighted path must
remain **behaviorally and memory identical** to today.

Reference: `ben-manes/caffeine` `BoundedLocalCache.java` (commit `30f867a`), `Weigher.java`,
node/cache code generators (`AddMaximum.java`).

## 2. How the current (unweighted) LFU works  (baseline to preserve)
- `ConcurrentLfuCore<K,V,N,P,E>` holds `windowLru`/`probationLru`/`protectedLru`
  (`LfuNodeList<K,V>`), a `CmSketch`, and an `LfuCapacityPartition` (integer
  Window/Protected/Probation counts).
- Reads/writes are buffered (`readBuffer`/`writeBuffer`) then applied under
  `maintenanceLock` in `Maintenance()` -> `OnAccess`/`OnWrite`, then `EvictEntries()`,
  then `capacity.OptimizePartitioning(...)`, then `ReFitProtected()`.
- All sizing is **count-based**: `EvictFromWindow` runs `while windowLru.Count >
  capacity.Window`; `EvictFromMain` runs `while window+probation+protected.Count > Capacity`.
- Hill-climb is **ratio-based**: `OptimizePartitioning` nudges a `mainRatio` double from
  hit-rate change and recomputes integer (window, protected, probation). It does **not**
  touch the LRU lists; the count loops + `ReFitProtected`/`PromoteProbation` lazily enforce
  the new target counts.
- `OnWrite` handles add **and** update in one path keyed off `node.Position` + `node.list ==
  null`, and **always** promotes a probation entry on update (`PromoteProbation`).
- `AdmitCandidate` currently only does `candidateFreq > victimFreq` (has a `// TODO: random
  factor` at `ConcurrentLfuCore.cs:891`).
- Exact eviction order and partition behavior are locked by tests
  (`LfuCapacityPartitionTests`, the commented W/P/Probation assertions in `ConcurrentLfuTests`).

## 3. Caffeine weighted algorithm (the target) — confirmed from source
- **Per-node**: `weight` (user value, set synchronously at write) and `policyWeight`
  (policy's view, set during maintenance; **0 until the add drains**). Eviction reads
  `policyWeight`. `makeDead` (our `Evict`) subtracts **`weight`**, not `policyWeight`.
- **Cache sizes** (`long`): `weightedSize`, `windowWeightedSize`,
  `mainProtectedWeightedSize`. Probation weight is implicit
  (`weightedSize - window - protected`).
- **Maximums** (`long`): `maximum`, `windowMaximum` (~1%), `mainProtectedMaximum`
  (~79.2% of total = 80% of main). Climb adjusts **absolute weight quotas**.
- **evictFromWindow**: `while windowWeightedSize > windowMaximum`, move LRU window node to
  **probation tail**; **skip `policyWeight == 0`** nodes; decrement `windowWeightedSize` by
  `policyWeight`; return first moved node as the starting candidate.
- **evictFromMain(candidate)**: `while weightedSize > maximum`. `victim` = probation LRU,
  advancing toward MRU; `candidate` = window-promoted nodes, then window LRU. Skip
  zero-weight; if only one present evict it; if same node evict; **if
  `candidate.policyWeight > maximum` evict candidate**; else `admit()` decides. When both
  exhausted, fall back to protected LRU, then window LRU, then break.
- **admit(cand, victim)**: `candFreq > victimFreq` -> admit; else if `candFreq >= 6` ->
  `1/128` random admit (anti hash-flood); else keep victim (ties favor the main-space victim).
- **Oversize on add** (after map insert, sketch incremented): `weight > maximum` -> evict
  immediately; `weight > windowMaximum` -> insert at window **LRU front** (`offerFirst`);
  else MRU tail.
- **Update changing weight** (`delta = newWeight - oldWeight`): apply delta to sizes; window
  grow `> maximum` -> evict, grow `> windowMaximum` -> **move to LRU front**; probation/
  protected grow `> maximum` -> evict; shrink -> `onAccess` (may promote). A probation entry
  whose `policyWeight > mainProtectedMaximum` is **not** promoted (kept in probation).
- **Zero weight**: legal; a size-eviction targeting a `weight == 0` node **resurrects** it;
  zero-weight nodes are skipped by the evict loops and never size-evicted.
- **Hill climb** (weighted): `determineAdjustment()` produces a signed weight `adjustment`
  from hit-rate change; `increaseWindow()`/`decreaseWindow()` transfer budget between
  `windowMaximum` and `mainProtectedMaximum` **and physically move whole nodes** between the
  deques within that quota (cap `QUEUE_TRANSFER_THRESHOLD = 1000` moves/step), returning
  unused quota. Protected enforcement is `mainProtectedWeightedSize > mainProtectedMaximum`.
- **Sketch sizing**: weighted caches size the frequency sketch by **entry count**, not by the
  weight maximum.

## 4. Design principles
1. **Unweighted path untouched** — no behavior change, no node-layout change, no extra hot-path
   work. Existing tests must pass unmodified.
2. **Compile-time selection** of weighted vs count via BitFaster's established patterns
   (generic struct policies + `static readonly bool` JIT elision, e.g.
   `EventPolicyDispatch.IsEnabled`, `FastConcurrentLfu.IsExpireAfterPolicy`).
3. **Additive public API only** (`IBoundedPolicy.Capacity` stays `int`).
4. **Faithful port** of the weighted accounting, eviction, admission, oversize, update, and
   hill-climb behavior.

## 5. Architecture — weight via `INodePolicy`  (DECIDED: no new generic)
Per maintainer decision (D1), weighting is carried by the existing node policy `P`;
`ConcurrentLfuCore` keeps its `<K,V,N,P,E>` arity. **No 6th generic, no `ISizePolicy`.**

Two new `INodePolicy` structs are added alongside the existing `AccessOrderPolicy` /
`ExpireAfterPolicy`:
- `WeightedAccessOrderPolicy<K,V,E>` — weighted, no expiry.
- `WeightedExpireAfterPolicy<K,V,E>` — weighted + time expiry; **composes** the existing
  `ExpireAfterPolicy` for the expiry hooks (TimerWheel scheduling) and layers weight on top, to
  avoid duplicating expiry logic.

**Selection & elision:** add `bool IsWeighted { get; }` to `INodePolicy` (a literal
`true`/`false`), and in the core use `static readonly bool IsWeighted = default(P).IsWeighted;`
(safe — the getter is a pure literal). The eviction/climb code branches on this static bool;
for the unweighted policies the JIT elides the weighted branches so the current path is
untouched (same idiom as `FastConcurrentLfu.IsExpireAfterPolicy`,
`EventPolicyDispatch.IsEnabled`).

**Sizing seam added to `INodePolicy`** — implemented trivially by the two unweighted policies
(constants / no-ops, elided) and substantively by the two weighted policies:
- `int Weigh(K, V)` — unweighted returns 1.
- `bool IsWeighted { get; }`.
- per-node weight access on `N`: `int GetPolicyWeight(N)`, `void SetPolicyWeight(N, int)`,
  `int GetWeight(N)`, `void SetWeight(N, int)`. Unweighted = `return 1` / no-op; weighted reads
  the node's fields **directly** (for a weighted cache `N` *is* the weighted node type — matched
  pair, see §6 — so no cast, no virtual dispatch).
- **Weighted cache-wide state** (the `long` `weightedSize` / `windowWeightedSize` /
  `mainProtectedWeightedSize`, the weighted maximums, and the climb) lives **inside the weighted
  policy struct** (mutable struct field, like `eventPolicy`). The core reads/updates it via
  `policy.` members and drives the node-moving climb by passing itself back in
  (e.g. `policy.Optimize(ref this, ...)`), mirroring the existing `ExpireEntries(ref this)`
  reach-back pattern. For the unweighted policies these members are empty/no-ops.

**Benefit of D1 vs the rejected 6th-generic option:** `INodePolicy.ExpireEntries`,
`TimerWheel.Advance`/`Expire`, and the wrapper core field arities **do not change** (no `S`
churn). **Cost:** the `INodePolicy` interface grows (all four policy structs implement the new
members) and there are two new weighted `P` structs (only +2 — expiry's afterWrite/afterAccess/
custom modes are already collapsed into the single `ExpireAfterPolicy` via its
`IExpiryCalculator`, so there is no real combinatorial explosion).

## 6. Node weight storage  (DECIDED: weighted node subclasses)  — see §12 D2
**Weighted node subclasses** `WeightedAccessOrderNode<K,V>` and `WeightedTimeOrderNode<K,V>`
`WeightedTimeOrderNode<K,V>` adding `int weight` + `int policyWeight`. The unweighted nodes and
their memory layout are **untouched** (no +8 bytes tax). The factory wires **matched (N, P)
pairs** — `(WeightedAccessOrderNode, WeightedAccessOrderPolicy)` and
`(WeightedTimeOrderNode, WeightedExpireAfterPolicy)` — so the weighted policy's weight accessors
read the node fields directly with no cast/virtual.

**Alternative:** put `Weight`/`PolicyWeight` on base `LfuNode<K,V>` (+8 bytes/node always,
simpler code). Rejected by default because BitFaster is memory-first and the unweighted path
should pay nothing (per critique).

## 7. Public API & builder
- New `IWeigher<K, V>` interface (mirrors `IExpiryCalculator<K,V>`): `int Weigh(K key, V value)`.
  Document the non-negative contract; throw `ArgumentOutOfRangeException` on negative (mirrors
  Caffeine's `BoundedWeigher`). Provide a `Weigher` helper with a singleton (weight 1) and a
  `Func<K,V,int>` adapter for ergonomics.
- `ConcurrentLfuBuilder<K,V>.WithWeigher(IWeigher<K,V> weigher)` -> stores on `LfuInfo<K>`.
  (Consider `LfuInfo<K>` becoming weigher-aware similar to its `expiry` handling.)
- `ConcurrentLfuFactory.Create` switch gains a `weigher != null` dimension and selects the
  weighted `N`/`P` pair. Weight composes with events and with time expiry.
- **Wrapper wiring:** the public `ConcurrentLfu<K,V>` hard-codes an unweighted core type, so
  weighted+events needs its own wrapper that exposes `Events` — mirror the existing internal
  `ConcurrentTLfu<K,V>` pattern (a new internal weighted wrapper, and/or generalize
  `ConcurrentTLfu` to the weighted+time+events combo). Weighted **without** events maps cleanly
  onto the already-generic `FastConcurrentLfu<K,V,N,P>` with the weighted `N`/`P` args.
- `WithCapacity` doc updated: "maximum total **weight** when a weigher is configured."
- `IBoundedPolicy.Capacity` stays `int` (weight budget, capped at `int.MaxValue`); internal
  accounting uses `long` to avoid overflow. Document that with a weigher `Count` may exceed
  `Capacity` (zero/low-weight entries).

## 8. File-by-file changes
**New files**
- `Lfu/IWeigher.cs` — `IWeigher<K,V>` + `Weigher` helpers (singleton, `Func` adapter).
- `Lfu/WeightedNodePolicy.cs` — `WeightedAccessOrderPolicy` and `WeightedExpireAfterPolicy`
  (each holds the `IWeigher` + the weighted `long` state/maximums + accounting hooks +
  admission + climb; the expiry one composes `ExpireAfterPolicy`).
- `Lfu/WeightedLfuNode.cs` — `WeightedAccessOrderNode`, `WeightedTimeOrderNode` (if §12 D2 =
  subclasses).
- (Maybe) `Lfu/WeightedCapacityPartition.cs` — `long` maximum/windowMaximum/
  mainProtectedMaximum + `determineAdjustment`, owned by the weighted policy struct; the
  node-moving increase/decrease runs in the core via a `policy.Optimize(ref this, …)` reach-back.
- (Maybe) internal weighted cache wrapper for the weighted+events (and weighted+time+events)
  combos — see §7.

**Modified**
- `Lfu/NodePolicy.cs` — `INodePolicy` interface grows the sizing seam (`IsWeighted`, `Weigh`,
  `GetPolicyWeight`/`SetPolicyWeight`/`GetWeight`/`SetWeight`, and the size/climb reach-back
  members); `AccessOrderPolicy` + `ExpireAfterPolicy` implement them trivially (constants/
  no-ops). **`ExpireEntries` keeps its current generic arity** (no `S`).
- `Lfu/ConcurrentLfuCore.cs` — `static readonly bool IsWeighted`; weighted branches in
  `OnWrite` (split add/update/remove delta logic), `Evict`, `EvictEntries`, `EvictFromWindow`,
  `EvictFromMain`, `AdmitCandidate`, `Trim`; weighted protected enforcement replacing
  `ReFitProtected` for weighted; route size reads/writes through `policy.`; sketch sized by
  count when weighted; `Capacity` clamp. **No new generic param.**
- `Lfu/TimerWheel.cs` — **unchanged** (arity stays the same; this is a benefit of D1).
- `Lfu/LfuNodeList.cs` — add `AddFirst(node)` / `MoveToFront(node)` primitives (needed for
  oversize-add `offerFirst` and update `moveToFront`).
- `Lfu/LfuNode.cs` — only if §12 D2 = base fields.
- `Lfu/ConcurrentTLfu.cs` (+ any new weighted wrapper), `Lfu/FastConcurrentLfu.cs` — accept the
  weighted `N`/`P` type args; `ConcurrentLfu.cs` is unchanged unless a weighted+events wrapper
  reuses it.
- `Lfu/Builder/LfuInfo.cs`, `Lfu/ConcurrentLfuBuilder.cs` (+ `LfuBuilderBase` if exposed
  broadly), `ConcurrentLfuBuilderExtensions.cs` — `WithWeigher` + factory wiring.
- README / API docs — document weighted eviction + capacity semantics.

## 9. Algorithm port details (the crux)
**Accounting is `PolicyWeight`-authoritative (avoids buffer double-count):**
- New add (in `OnWrite`, `node.list == null && !WasRemoved`): `delta = Weight`;
  `weightedSize += delta`; `windowWeightedSize += delta`; set `PolicyWeight = Weight`.
- Existing update: `delta = Weight - PolicyWeight`; apply delta to `weightedSize` and the
  node's current-queue size; set `PolicyWeight = Weight`. **Do not** auto-`PromoteProbation`;
  instead follow Caffeine's conditional grow/shrink/evict/move-to-front logic.
- Remove/evict path (`WasRemoved`, and `Evict`): subtract the **accounted** amount
  (`PolicyWeight`/queue-specific) — never blindly subtract `Weight` for a node whose add never
  drained (guards negative accounting). On real eviction, subtract `Weight` (finalized) per
  Caffeine `makeDead`.
- Duplicate buffered writes for one node must net to a single correct delta because each apply
  recomputes `Weight - PolicyWeight` and then syncs `PolicyWeight`.
- Oversize-on-add: implement the `> maximum` evict / `> windowMaximum` `AddFirst` / else
  `AddLast` placement.
- Zero-weight: skip in evict loops; **resurrect** on size-eviction of a `weight == 0` node.
- `EvictFromWindow`/`EvictFromMain`/`AdmitCandidate`: weighted variants per §3 (skip-zero,
  candidate-oversize evict, freq compare + `>= 6` `1/128` random). The random admission is
  implemented in the **weighted** path only; the existing unweighted admission is left unchanged
  (D4 deferred).
- **Weighted hill-climb**: the weighted policy's `DetermineAdjustment(metrics, sampleSize)`
  returns a signed weight adjustment; the core applies `IncreaseWindow`/`DecreaseWindow` (via a
  `policy.Optimize(ref this, …)` reach-back) that move whole nodes between deques within quota
  (cap 1000), updating window/protected sizes & maximums, then a weighted `DemoteFromProtected`.
  The count path keeps `OptimizePartitioning` + `ReFitProtected` unchanged.

## 10. Test strategy  (`BitFaster.Caching.UnitTests/Lfu/`, one test file per class)
- **Builder**: `WithWeigher` builds a weighted cache; negative weight throws; composes with
  events; capacity is weight budget.
- **Accounting/eviction parity** (drive with `DoMaintenance()` + a `LogLru`-style weighted
  dump): basic weighted eviction by total weight; window/probation/protected weighted splits;
  admission (freq compare) chooses correctly.
- **Caffeine edge cases**: entry `weight > maximum` evicted on add; `weight > windowMaximum`
  placed at window front and leaves next; update grows weight -> evict/move-to-front; update
  shrinks -> promote; zero-weight entries never size-evicted and allow `Count > Capacity`;
  probation entry with `policyWeight > mainProtectedMaximum` stays in probation.
- **Buffer-model edge cases** (where BitFaster differs from Caffeine's task model): add then
  remove before maintenance; add then multiple weight updates before first drain; remove a node
  with `policyWeight == 0`; zero-weight node at an LRU head during eviction; expired weighted
  node subtracts size exactly once; duplicate write-buffer entries don't double-count.
- **Weighted climb**: window grows/shrinks in weight units; converges; respects the
  window>=1 / protected>=0 bounds; transfer cap honored.
- **Soak**: extend `ConcurrentLfuSoakTests` with a weighted variant asserting weighted-size
  invariants (`weightedSize == sum(node.weight)`, `window+protected+probation` consistency)
  hold under concurrency.
- **Hit-rate parity (optional)**: a weighted scenario in `HitRateAnalysis` cross-checked
  against Caffeine numbers.

## 11. Phased delivery
1. **Foundations** — `IWeigher`, `WithWeigher`, factory wiring, the `INodePolicy` sizing seam +
   the two new weighted policy structs as no-op-until-wired stubs (no behavior change), weighted
   node type(s), `LfuNodeList.AddFirst`. Gate: full build (all TFMs) + **all existing tests
   green**, weighted cache builds and behaves like count.
2. **Weighted accounting** — `long` sizes/maximums (in the weighted policy), `OnWrite`
   add/update/remove delta, `Evict` subtract, oversize-on-add, zero-weight resurrect.
3. **Weighted eviction** — `EvictFromWindow`/`EvictFromMain`/`AdmitCandidate` weighted +
   randomness; weighted protected enforcement.
4. **Weighted climb** — `DetermineAdjustment` + node-moving increase/decrease via the
   `policy.Optimize(ref this, …)` reach-back.
5. **Variants (v1 scope)** — `ConcurrentTLfu` (+ any new weighted+events wrapper) so weight
   composes with time expiry (`WeightedTimeOrderNode` + `WeightedExpireAfterPolicy` together;
   factory handles weigher × {none, afterWrite, afterAccess, custom expiry}), and weighted `Trim`
   semantics. **Deferred to a later milestone:** `FastConcurrentLfu` standalone exposure and the
   scoped/atomic/async builder wrappers.
6. **Tests + validation + docs** — full matrix above; `dotnet format`; soak; README.

**Verified commands**: build `dotnet build BitFaster.Caching\BitFaster.Caching.csproj -c
Release` (multi-TFM: netstandard2.0, netcoreapp3.1, net6.0, net10.0). Run `dotnet format`
after changes (repo convention). Tests via the `BitFaster.Caching.UnitTests` project.

## 12. Open decisions for the maintainer
- **D1 Seam** *(DECIDED: weight is carried by the existing `INodePolicy` slot — two new weighted
  policy structs, no new generic. `ConcurrentLfuCore` arity and `TimerWheel`/`ExpireEntries`
  signatures are unchanged; the `INodePolicy` interface grows a sizing seam. See §5.)*
- **D2 Node memory** *(DECIDED: weighted node subclasses `WeightedAccessOrderNode` /
  `WeightedTimeOrderNode`; unweighted nodes and their layout are untouched — zero memory tax on
  the default path.)*
- **D3 Scope** *(DECIDED: Core `ConcurrentLfu` + `ConcurrentTLfu` so weight composes with time
  expiry — matches Caffeine. `FastConcurrentLfu` and scoped/atomic/async wrappers deferred.)*
- **D4 Unweighted admission TODO** *(DEFERRED: leave the existing unweighted admission as-is for
  now; implement the `>=6` / `1/128` random admission only in the weighted path. Finishing the
  unweighted TODO — which would shift unweighted eviction order/tests — is a separate later
  task.)*

## 13. Risks
- Buffered duplicate writes/removes -> double/under-count (mitigated by `PolicyWeight`-based
  deltas).
- `INodePolicy` interface growth: all four policy structs must implement the new sizing-seam
  members (unweighted ones trivially); mechanical and compiler-enforced.
- Accidental behavior change to the unweighted path (mitigated by compile-time elision +
  existing tests as a guard).
- `int` capacity vs `long` internal accounting overflow (clamp + `long` math).
