﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lru
{
    public struct NoTelemetryPolicy<K, V> : ITelemetryPolicy<K, V>
    {
        public double HitRatio => 0.0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IncrementMiss()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IncrementHit()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnItemRemoved(K key, V value, ItemRemovedReason reason)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetEventSource(object source)
        {
        }
    }
}
