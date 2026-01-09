using System;

namespace BitFaster.Caching.Lfu
{
    // Port TimerWheel from Caffeine
    // https://github.com/ben-manes/caffeine/blob/73d5011f9db373fc20a6e12d1f194f0d7a967d69/caffeine/src/main/java/com/github/benmanes/caffeine/cache/TimerWheel.java#L36
    // This is separate to avoid in memory dupes due to generics
    internal static class TimerWheel
    {
        internal static readonly int[] Buckets = { 64, 64, 32, 4, 1 };

        internal static readonly long[] Spans = {
            BitOps.CeilingPowerOfTwo(Duration.FromSeconds(1).raw), // 1.07s
            BitOps.CeilingPowerOfTwo(Duration.FromMinutes(1).raw), // 1.14m
            BitOps.CeilingPowerOfTwo(Duration.FromMinutes(60).raw),   // 1.22h
            BitOps.CeilingPowerOfTwo(Duration.FromMinutes(60*24).raw),    // 1.63d
            Buckets[3] * BitOps.CeilingPowerOfTwo(Duration.FromMinutes(60*24*6).raw), // 6.5d
            Buckets[3] * BitOps.CeilingPowerOfTwo(Duration.FromMinutes(60*24*6).raw), // 6.5d
        };

        internal static readonly int[] Shift = {
            BitOps.TrailingZeroCount(Spans[0]),
            BitOps.TrailingZeroCount(Spans[1]),
            BitOps.TrailingZeroCount(Spans[2]),
            BitOps.TrailingZeroCount(Spans[3]),
            BitOps.TrailingZeroCount(Spans[4]),
        };
    }

    // A hierarchical timer wheel to add, remove, and fire expiration events in amortized O(1) time. The
    // expiration events are deferred until the timer is advanced, which is performed as part of the
    // cache's maintenance cycle.
    //
    // This is a direct port of TimerWheel from Java's Caffeine.
    // @author ben.manes@gmail.com (Ben Manes)
    // https://github.com/ben-manes/caffeine/blob/master/caffeine/src/main/java/com/github/benmanes/caffeine/cache/TimerWheel.java
    internal sealed class TimerWheel<K, V>
        where K : notnull
    {
        internal readonly TimeOrderNode<K, V>[][] wheels;

        internal long time;

        public TimerWheel()
        {
            wheels = new TimeOrderNode<K, V>[TimerWheel.Buckets.Length][];

            for (int i = 0; i < wheels.Length; i++)
            {
                wheels[i] = new TimeOrderNode<K, V>[TimerWheel.Buckets[i]];

                for (int j = 0; j < wheels[i].Length; j++)
                {
                    wheels[i][j] = TimeOrderNode<K, V>.CreateSentinel();
                }
            }
        }

        /// <summary>
        ///  Advances the timer and evicts entries that have expired.
        /// </summary>
        /// <param name="cache"></param>
        /// <param name="currentTime"></param>
        public void Advance<N, P>(ref ConcurrentLfuCore<K, V, N, P> cache, Duration currentTime)
            where N : LfuNode<K, V>
            where P : struct, INodePolicy<K, V, N>
        {
            long previousTime = time;
            time = currentTime.raw;

            // If wrapping then temporarily shift the clock for a positive comparison. We assume that the
            // advancements never exceed a total running time of long.MaxValue nanoseconds (292 years)
            // so that an overflow only occurs due to using an arbitrary origin time.
            if ((previousTime < 0) && (currentTime > Duration.Zero))
            {
                previousTime += long.MaxValue;
                currentTime += new Duration(long.MaxValue);
            }

            try
            {
                for (int i = 0; i < TimerWheel.Shift.Length; i++)
                {
                    long previousTicks = (long)(((ulong)previousTime) >> TimerWheel.Shift[i]);
                    long currentTicks = (long)(((ulong)currentTime.raw) >> TimerWheel.Shift[i]);
                    long delta = (currentTicks - previousTicks);

                    if (delta <= 0L)
                    {
                        break;
                    }

                    Expire(ref cache, i, previousTicks, delta);
                }
            }
            catch (Exception)
            {
                time = previousTime;
                throw;
            }
        }

        // Expires entries or reschedules into the proper bucket if still active.
        private void Expire<N, P>(ref ConcurrentLfuCore<K, V, N, P> cache, int index, long previousTicks, long delta)
            where N : LfuNode<K, V>
            where P : struct, INodePolicy<K, V, N>
        {
            TimeOrderNode<K, V>[] timerWheel = wheels[index];
            int mask = timerWheel.Length - 1;

            // We assume that the delta does not overflow an integer and cause negative steps. This can
            // occur only if the advancement exceeds 2^61 nanoseconds (73 years).
            int steps = Math.Min(1 + (int)delta, timerWheel.Length);
            int start = (int)(previousTicks & mask);
            int end = start + steps;

            for (int i = start; i < end; i++)
            {
                TimeOrderNode<K, V> sentinel = timerWheel[i & mask];
                TimeOrderNode<K, V> prev = sentinel.GetPreviousInTimeOrder();
                TimeOrderNode<K, V> node = sentinel.GetNextInTimeOrder();
                sentinel.SetPreviousInTimeOrder(sentinel);
                sentinel.SetNextInTimeOrder(sentinel);

                while (node != sentinel)
                {
                    TimeOrderNode<K, V> next = node.GetNextInTimeOrder();
                    node.SetPreviousInTimeOrder(null);
                    node.SetNextInTimeOrder(null);

                    try
                    {
                        if ((node.GetTimestamp() - time) < 0)
                        {
                            cache.Evict(node);
                        }
                        else
                        {
                            Schedule(node);
                        }
                        node = next;
                    }
                    catch (Exception)
                    {
                        node.SetPreviousInTimeOrder(sentinel.GetPreviousInTimeOrder());
                        node.SetNextInTimeOrder(next);
                        sentinel.GetPreviousInTimeOrder().SetNextInTimeOrder(node);
                        sentinel.SetPreviousInTimeOrder(prev);
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Schedules a timer event for the node.
        /// </summary>
        /// <param name="node"></param>
        public void Schedule(TimeOrderNode<K, V> node)
        {
            TimeOrderNode<K, V> sentinel = FindBucket(node.GetTimestamp());
            Link(sentinel, node);
        }

        /// <summary>
        /// Reschedules an active timer event for the node.
        /// </summary>
        /// <param name="node"></param>
        public void Reschedule(TimeOrderNode<K, V> node)
        {
            if (node.GetNextInTimeOrder() != null)
            {
                Unlink(node);
                Schedule(node);
            }
        }

        /// <summary>
        /// Removes a timer event for this entry if present.
        /// </summary>
        /// <param name="node"></param>
        public static void Deschedule(TimeOrderNode<K, V> node)
        {
            Unlink(node);
            node.SetNextInTimeOrder(null);
            node.SetPreviousInTimeOrder(null);
        }

        // Determines the bucket that the timer event should be added to.
        private TimeOrderNode<K, V> FindBucket(long time)
        {
            long duration = time - this.time;
            int length = wheels.Length - 1;

            for (int i = 0; i < length; i++)
            {
                if (duration < TimerWheel.Spans[i + 1])
                {
                    long ticks = (long)((ulong)time >> TimerWheel.Shift[i]);
                    int index = (int)(ticks & (wheels[i].Length - 1));

                    return wheels[i][index];
                }
            }

            return wheels[length][0];
        }

        // Adds the entry at the tail of the bucket's list.
        private static void Link(TimeOrderNode<K, V> sentinel, TimeOrderNode<K, V> node)
        {
            node.SetPreviousInTimeOrder(sentinel.GetPreviousInTimeOrder());
            node.SetNextInTimeOrder(sentinel);

            sentinel.GetPreviousInTimeOrder().SetNextInTimeOrder(node);
            sentinel.SetPreviousInTimeOrder(node);
        }

        // Removes the entry from its bucket, if scheduled.
        private static void Unlink(TimeOrderNode<K, V> node)
        {
            TimeOrderNode<K, V> next = node.GetNextInTimeOrder();

            if (next != null)
            {
                TimeOrderNode<K, V> prev = node.GetPreviousInTimeOrder();
                next.SetPreviousInTimeOrder(prev);
                prev.SetNextInTimeOrder(next);
            }
        }

        // Returns the duration until the next bucket expires, or long.MaxValue if none.
        public Duration GetExpirationDelay()
        {
            for (int i = 0; i < TimerWheel.Shift.Length; i++)
            {
                TimeOrderNode<K, V>[] timerWheel = wheels[i];
                long ticks = (long)((ulong)time >> TimerWheel.Shift[i]);

                long spanMask = TimerWheel.Spans[i] - 1;
                int start = (int)(ticks & spanMask);
                int end = start + timerWheel.Length;
                int mask = timerWheel.Length - 1;

                for (int j = start; j < end; j++)
                {
                    TimeOrderNode<K, V> sentinel = timerWheel[(j & mask)];
                    TimeOrderNode<K, V> next = sentinel.GetNextInTimeOrder();

                    if (next == sentinel)
                    {
                        continue;
                    }

                    long buckets = (j - start);
                    long delay = (buckets << TimerWheel.Shift[i]) - (time & spanMask);
                    delay = (delay > 0) ? delay : TimerWheel.Spans[i];

                    for (int k = i + 1; k < TimerWheel.Shift.Length; k++)
                    {
                        long nextDelay = PeekAhead(k);
                        delay = Math.Min(delay, nextDelay);
                    }

                    return new Duration(delay);
                }
            }

            return new Duration(long.MaxValue);
        }

        // Returns the duration when the wheel's next bucket expires, or long.MaxValue if empty.
        private long PeekAhead(int index)
        {
            long ticks = (long)((ulong)time >> TimerWheel.Shift[index]);
            TimeOrderNode<K, V>[] timerWheel = wheels[index];

            long spanMask = TimerWheel.Spans[index] - 1;
            int mask = timerWheel.Length - 1;
            int probe = (int)((ticks + 1) & mask);
            TimeOrderNode<K, V> sentinel = timerWheel[probe];
            TimeOrderNode<K, V> next = sentinel.GetNextInTimeOrder();

            return (next == sentinel) ? long.MaxValue : (TimerWheel.Spans[index] - (time & spanMask));
        }
    }
}
