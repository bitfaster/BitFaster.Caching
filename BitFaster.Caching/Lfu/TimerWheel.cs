using System;

namespace BitFaster.Caching.Lfu
{
    // Port TimerWheel from Caffeine
    // https://github.com/ben-manes/caffeine/blob/73d5011f9db373fc20a6e12d1f194f0d7a967d69/caffeine/src/main/java/com/github/benmanes/caffeine/cache/TimerWheel.java#L36
    internal class TimerWheel<K, V>
        where K : notnull
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

        private static readonly int[] Shift = {
            BitOps.TrailingZeroCount(Spans[0]),
            BitOps.TrailingZeroCount(Spans[1]),
            BitOps.TrailingZeroCount(Spans[2]),
            BitOps.TrailingZeroCount(Spans[3]),
            BitOps.TrailingZeroCount(Spans[4]),
        };

        private readonly TimeOrderNode<K, V>[][] wheel;

        internal long time;

        public TimerWheel()
        {
            wheel = new TimeOrderNode<K, V>[Buckets.Length][];

            for (int i = 0; i < wheel.Length; i++)
            {
                wheel[i] = new TimeOrderNode<K, V>[Buckets[i]];

                for (int j = 0; j < wheel[i].Length; j++)
                {
                    wheel[i][j] = TimeOrderNode< K, V>.CreateSentinel();
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
            // advancements never exceed a total running time of Long.MAX_VALUE nanoseconds (292 years)
            // so that an overflow only occurs due to using an arbitrary origin time (System.nanoTime()).
            if ((previousTime < 0) && (currentTime > Duration.Zero))
            {
                previousTime += long.MaxValue;
                currentTime += new Duration(long.MaxValue);
            }

            try
            {
                for (int i = 0; i < Shift.Length; i++)
                {
                    long previousTicks = (long)(((ulong)previousTime) >> Shift[i]);
                    long currentTicks = (long)(((ulong)currentTime.raw) >> Shift[i]);
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
            TimeOrderNode<K, V>[] timerWheel = wheel[index];
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
                        // TODO: Caffeine passes the time into evict here, and can resurrect
                        // https://github.com/ben-manes/caffeine/blob/73d5011f9db373fc20a6e12d1f194f0d7a967d69/caffeine/src/main/java/com/github/benmanes/caffeine/cache/BoundedLocalCache.java#L1023
                        if ((node.GetTimestamp() - time) < 0)
                        {
                            cache.Evict(node);
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
            int length = wheel.Length - 1;

            for (int i = 0; i < length; i++)
            {
                if (duration < Spans[i + 1])
                {
                    long ticks = (long)((ulong)time >> Shift[i]);
                    int index = (int)(ticks & (wheel[i].Length - 1));

                    return wheel[i][index];
                }
            }

            return wheel[length][0];
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
            for (int i = 0; i < Shift.Length; i++)
            {
                TimeOrderNode<K, V>[] timerWheel = wheel[i];
                long ticks = (long)((ulong)time >> Shift[i]);

                long spanMask = Spans[i] - 1;
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
                    long delay = (buckets << Shift[i]) - (time & spanMask);
                    delay = (delay > 0) ? delay : Spans[i];

                    for (int k = i + 1; k < Shift.Length; k++)
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
            long ticks = (long)((ulong)time >> Shift[index]);
            TimeOrderNode<K, V>[] timerWheel = wheel[index];

            long spanMask = Spans[index] - 1;
            int mask = timerWheel.Length - 1;
            int probe = (int)((ticks + 1) & mask);
            TimeOrderNode<K, V> sentinel = timerWheel[probe];
            TimeOrderNode<K, V> next = sentinel.GetNextInTimeOrder();

            return (next == sentinel) ? long.MaxValue: (Spans[index] - (time & spanMask));
        }
    }
}
