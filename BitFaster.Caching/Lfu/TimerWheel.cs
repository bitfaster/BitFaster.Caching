using System;

namespace BitFaster.Caching.Lfu
{
    // Port TimerWheel from Caffeine
    // https://github.com/ben-manes/caffeine/blob/73d5011f9db373fc20a6e12d1f194f0d7a967d69/caffeine/src/main/java/com/github/benmanes/caffeine/cache/TimerWheel.java#L36
    internal class TimerWheel<K, V>
    {
        static readonly int[] BUCKETS = { 64, 64, 32, 4, 1 };

        static readonly long[] SPANS = {
            BitOps.CeilingPowerOfTwo(Duration.FromSeconds(1).raw), // 1.07s
            BitOps.CeilingPowerOfTwo(Duration.FromMinutes(1).raw), // 1.14m
            BitOps.CeilingPowerOfTwo(Duration.FromMinutes(60).raw),   // 1.22h
            BitOps.CeilingPowerOfTwo(Duration.FromMinutes(60*24).raw),    // 1.63d
            BUCKETS[3] * BitOps.CeilingPowerOfTwo(Duration.FromMinutes(60*24).raw), // 6.5d
            BUCKETS[3] * BitOps.CeilingPowerOfTwo(Duration.FromMinutes(60*24).raw), // 6.5d
        };

        static readonly int[] SHIFT = {
            BitOps.TrailingZeroCount(SPANS[0]),
            BitOps.TrailingZeroCount(SPANS[1]),
            BitOps.TrailingZeroCount(SPANS[2]),
            BitOps.TrailingZeroCount(SPANS[3]),
            BitOps.TrailingZeroCount(SPANS[4]),
        };

        private readonly TimeOrderNode<K, V>[][] wheel;

        // TODO: replace with Duration
        long nanos;

        public TimerWheel()
        {
            wheel = new TimeOrderNode<K, V>[BUCKETS.Length][];
            for (int i = 0; i < wheel.Length; i++)
            {
                wheel[i] = new TimeOrderNode<K, V>[BUCKETS[i]];
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
        /// <param name="currentTimeNanos"></param>
        public void Advance(ConcurrentLfu<K, V> cache, long currentTimeNanos)
        {
            long previousTimeNanos = nanos;
            nanos = currentTimeNanos;

            // TODO: handle wrapping
            // If wrapping then temporarily shift the clock for a positive comparison. We assume that the
            // advancements never exceed a total running time of Long.MAX_VALUE nanoseconds (292 years)
            // so that an overflow only occurs due to using an arbitrary origin time (System.nanoTime()).
            //if ((previousTimeNanos < 0) && (currentTimeNanos > 0))
            //{
            //    previousTimeNanos += Long.MAX_VALUE;
            //    currentTimeNanos += Long.MAX_VALUE;
            //}

            try
            {
                for (int i = 0; i < SHIFT.Length; i++)
                {
                    long previousTicks = (long)(((ulong)previousTimeNanos) >> SHIFT[i]);
                    long currentTicks = (long)(((ulong)currentTimeNanos) >> SHIFT[i]);
                    long delta = (currentTicks - previousTicks);
                    if (delta <= 0L)
                    {
                        break;
                    }
                    Expire(cache, i, previousTicks, delta);
                }
            }
            catch (Exception t)
            {
                nanos = previousTimeNanos;
                throw t;
            }
        }

        // Expires entries or reschedules into the proper bucket if still active.
        private void Expire(ConcurrentLfu<K, V> cache, int index, long previousTicks, long delta)
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
                TimeOrderNode<K, V> prev = sentinel.getPreviousInVariableOrder();
                TimeOrderNode<K, V> node = sentinel.getNextInVariableOrder();
                sentinel.setPreviousInVariableOrder(sentinel);
                sentinel.setNextInVariableOrder(sentinel);

                while (node != sentinel)
                {
                    TimeOrderNode<K, V> next = node.getNextInVariableOrder();
                    node.setPreviousInVariableOrder(null);
                    node.setNextInVariableOrder(null);

                    try
                    {
                        // TODO: Caffeine passes the time into evict here, and can resurrect
                        // https://github.com/ben-manes/caffeine/blob/73d5011f9db373fc20a6e12d1f194f0d7a967d69/caffeine/src/main/java/com/github/benmanes/caffeine/cache/BoundedLocalCache.java#L1023
                        if (((node.getVariableTime() - nanos) > 0))
                            //|| !cache.evictEntry(node, ItemRemovedReason.Expired, nanos))
                        {
                            cache.Evict(node);
                            //schedule(node);
                        }
                        node = next;
                    }
                    catch (Exception t)
                    {
                        node.setPreviousInVariableOrder(sentinel.getPreviousInVariableOrder());
                        node.setNextInVariableOrder(next);
                        sentinel.getPreviousInVariableOrder().setNextInVariableOrder(node);
                        sentinel.setPreviousInVariableOrder(prev);
                        throw t;
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
            TimeOrderNode<K, V> sentinel = FindBucket(node.getVariableTime());
            Link(sentinel, node);
        }

        /// <summary>
        /// Removes a timer event for this entry if present.
        /// </summary>
        /// <param name="node"></param>
        public void Deschedule(TimeOrderNode<K, V> node)
        {
            Unlink(node);
            node.setNextInVariableOrder(null);
            node.setPreviousInVariableOrder(null);
        }

        // Determines the bucket that the timer event should be added to.
        private TimeOrderNode<K, V> FindBucket(long time)
        {
            long duration = time - nanos;
            int length = wheel.Length - 1;
            for (int i = 0; i < length; i++)
            {
                if (duration < SPANS[i + 1])
                {
                    long ticks = (long)((ulong)time >> SHIFT[i]);
                    int index = (int)(ticks & (wheel[i].Length - 1));
                    return wheel[i][index];
                }
            }
            return wheel[length][0];
        }

        // Adds the entry at the tail of the bucket's list.
        private void Link(TimeOrderNode<K, V> sentinel, TimeOrderNode<K, V> node)
        {
            node.setPreviousInVariableOrder(sentinel.getPreviousInVariableOrder());
            node.setNextInVariableOrder(sentinel);

            sentinel.getPreviousInVariableOrder().setNextInVariableOrder(node);
            sentinel.setPreviousInVariableOrder(node);
        }

        // Removes the entry from its bucket, if scheduled.
        private void Unlink(TimeOrderNode<K, V> node)
        {
            TimeOrderNode<K, V> next = node.getNextInVariableOrder();
            if (next != null)
            {
                TimeOrderNode<K, V> prev = node.getPreviousInVariableOrder();
                next.setPreviousInVariableOrder(prev);
                prev.setNextInVariableOrder(next);
            }
        }

        // Returns the duration until the next bucket expires, or long.MaxValue if none.
        public long getExpirationDelay()
        {
            for (int i = 0; i < SHIFT.Length; i++)
            {
                TimeOrderNode<K, V>[] timerWheel = wheel[i];
                long ticks = (long)((ulong)nanos >> SHIFT[i]);

                long spanMask = SPANS[i] - 1;
                int start = (int)(ticks & spanMask);
                int end = start + timerWheel.Length;
                int mask = timerWheel.Length - 1;
                for (int j = start; j < end; j++)
                {
                    TimeOrderNode<K, V> sentinel = timerWheel[(j & mask)];
                    TimeOrderNode<K, V> next = sentinel.getNextInVariableOrder();
                    if (next == sentinel)
                    {
                        continue;
                    }
                    long buckets = (j - start);
                    long delay = (buckets << SHIFT[i]) - (nanos & spanMask);
                    delay = (delay > 0) ? delay : SPANS[i];

                    for (int k = i + 1; k < SHIFT.Length; k++)
                    {
                        long nextDelay = peekAhead(k);
                        delay = Math.Min(delay, nextDelay);
                    }

                    return delay;
                }
            }

            // TODO: revisit as Duration
            return long.MaxValue;
        }

        // Returns the duration when the wheel's next bucket expires, or long.MaxValue if empty.
        private long peekAhead(int index)
        {
            // TODO: revisit time as Duration
            long ticks = (long)((ulong)nanos >> SHIFT[index]);
            TimeOrderNode<K, V>[] timerWheel = wheel[index];

            long spanMask = SPANS[index] - 1;
            int mask = timerWheel.Length - 1;
            int probe = (int)((ticks + 1) & mask);
            TimeOrderNode<K, V> sentinel = timerWheel[probe];
            TimeOrderNode<K, V> next = sentinel.getNextInVariableOrder();
            return (next == sentinel) ? long.MaxValue: (SPANS[index] - (nanos & spanMask));
        }
    }
}
