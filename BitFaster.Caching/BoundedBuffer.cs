﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace BitFaster.Caching
{
    /// <summary>
    /// Provides a multi-producer, multi-consumer thread-safe bounded buffer.  When the buffer is full,
    /// adds fail and return false.  When the queue is empty, takes fail and return null.
    /// </summary>
    /// <remarks>
    /// Based on the Segment internal class from ConcurrentQueue
    /// https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Collections/Concurrent/ConcurrentQueueSegment.cs
    /// </remarks>
    public class BoundedBuffer<T>
    {
        private readonly Slot[] slots;

        private readonly int slotsMask;

        private PaddedHeadAndTail headAndTail;

        internal bool preservedForObservation;

        //bool _frozenForEnqueues;

        public BoundedBuffer(int boundedLength)
        {
            if (boundedLength < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(boundedLength));
            }

            boundedLength = BitOps.CeilingPowerOfTwo(boundedLength);

            // Initialize the slots and the mask.  The mask is used as a way of quickly doing "% _slots.Length",
            // instead letting us do "& _slotsMask".
            slots = new Slot[boundedLength];
            slotsMask = boundedLength - 1;

            // Initialize the sequence number for each slot.  The sequence number provides a ticket that
            // allows dequeuers to know whether they can dequeue and enqueuers to know whether they can
            // enqueue.  An enqueuer at position N can enqueue when the sequence number is N, and a dequeuer
            // for position N can dequeue when the sequence number is N + 1.  When an enqueuer is done writing
            // at position N, it sets the sequence number to N + 1 so that a dequeuer will be able to dequeue,
            // and when a dequeuer is done dequeueing at position N, it sets the sequence number to N + _slots.Length,
            // so that when an enqueuer loops around the slots, it'll find that the sequence number at
            // position N is N.  This also means that when an enqueuer finds that at position N the sequence
            // number is < N, there is still a value in that slot, i.e. the segment is full, and when a
            // dequeuer finds that the value in a slot is < N + 1, there is nothing currently available to
            // dequeue. (It is possible for multiple enqueuers to enqueue concurrently, writing into
            // subsequent slots, and to have the first enqueuer take longer, so that the slots for 1, 2, 3, etc.
            // may have values, but the 0th slot may still be being filled... in that case, TryDequeue will
            // return false.)
            for (var i = 0; i < slots.Length; i++)
            {
                slots[i].SequenceNumber = i;
            }
        }

        public int Count
        {
            get
            {
                var spinner = new SpinWait();
                while (true)
                {
                    var headNow = Volatile.Read(ref headAndTail.Head);
                    var tailNow = Volatile.Read(ref headAndTail.Tail);

                    if (headNow == Volatile.Read(ref headAndTail.Head) &&
                        tailNow == Volatile.Read(ref headAndTail.Tail))
                    {
                        return GetCount(headNow, tailNow);
                    }

                    spinner.SpinOnce();
                }
            }
        }

        private int GetCount(int head, int tail)
        {
            if (head != tail && head != tail - FreezeOffset)
            {
                head &= slotsMask;
                tail &= slotsMask;

                return head < tail ? tail - head : slots.Length - head + tail;
            }
            return 0;
        }

        public int Capacity => slots.Length;

        private int FreezeOffset => slots.Length * 2;

        public bool TryTake(out T item)
        {
            // Loop in case of contention...
            var spinner = new SpinWait();
            while (true)
            {
                // Get the head at which to try to dequeue.
                var currentHead = Volatile.Read(ref headAndTail.Head);
                var slotsIndex = currentHead & slotsMask;

                // Read the sequence number for the head position.
                var sequenceNumber = Volatile.Read(ref slots[slotsIndex].SequenceNumber);

                // We can dequeue from this slot if it's been filled by an enqueuer, which
                // would have left the sequence number at pos+1.
                var diff = sequenceNumber - (currentHead + 1);
                if (diff == 0)
                {
                    // We may be racing with other dequeuers.  Try to reserve the slot by incrementing
                    // the head.  Once we've done that, no one else will be able to read from this slot,
                    // and no enqueuer will be able to read from this slot until we've written the new
                    // sequence number. WARNING: The next few lines are not reliable on a runtime that
                    // supports thread aborts. If a thread abort were to sneak in after the CompareExchange
                    // but before the Volatile.Write, enqueuers trying to enqueue into this slot would
                    // spin indefinitely.  If this implementation is ever used on such a platform, this
                    // if block should be wrapped in a finally / prepared region.
                    if (Interlocked.CompareExchange(ref headAndTail.Head, currentHead + 1, currentHead) == currentHead)
                    {
                        // Successfully reserved the slot.  Note that after the above CompareExchange, other threads
                        // trying to dequeue from this slot will end up spinning until we do the subsequent Write.
                        item = slots[slotsIndex].Item;
                        if (!Volatile.Read(ref preservedForObservation))
                        {
                            // If we're preserving, though, we don't zero out the slot, as we need it for
                            // enumerations, peeking, ToArray, etc.  And we don't update the sequence number,
                            // so that an enqueuer will see it as full and be forced to move to a new segment.
                            slots[slotsIndex].Item = default;
                            Volatile.Write(ref slots[slotsIndex].SequenceNumber, currentHead + slots.Length);
                        }
                        return true;
                    }
                }
                else if (diff < 0)
                {
                    // The sequence number was less than what we needed, which means this slot doesn't
                    // yet contain a value we can dequeue, i.e. the segment is empty.  Technically it's
                    // possible that multiple enqueuers could have written concurrently, with those
                    // getting later slots actually finishing first, so there could be elements after
                    // this one that are available, but we need to dequeue in order.  So before declaring
                    // failure and that the segment is empty, we check the tail to see if we're actually
                    // empty or if we're just waiting for items in flight or after this one to become available.
                    //bool frozen = _frozenForEnqueues;
                    var currentTail = Volatile.Read(ref headAndTail.Tail);
                    if (currentTail - currentHead <= 0 || currentTail - FreezeOffset - currentHead <= 0)
                    {
                        item = default;
                        return false;
                    }

                    // It's possible it could have become frozen after we checked _frozenForEnqueues
                    // and before reading the tail.  That's ok: in that rare race condition, we just
                    // loop around again.
                }

                // Lost a race. Spin a bit, then try again.
                spinner.SpinOnce();
            }
        }

        public bool TryAdd(T item)
        {
            // Loop in case of contention...
            var spinner = new SpinWait();
            while (true)
            {
                // Get the tail at which to try to return.
                var currentTail = Volatile.Read(ref headAndTail.Tail);
                var slotsIndex = currentTail & slotsMask;

                // Read the sequence number for the tail position.
                var sequenceNumber = Volatile.Read(ref slots[slotsIndex].SequenceNumber);

                // The slot is empty and ready for us to enqueue into it if its sequence
                // number matches the slot.
                var diff = sequenceNumber - currentTail;
                if (diff == 0)
                {
                    // We may be racing with other enqueuers.  Try to reserve the slot by incrementing
                    // the tail.  Once we've done that, no one else will be able to write to this slot,
                    // and no dequeuer will be able to read from this slot until we've written the new
                    // sequence number. WARNING: The next few lines are not reliable on a runtime that
                    // supports thread aborts. If a thread abort were to sneak in after the CompareExchange
                    // but before the Volatile.Write, other threads will spin trying to access this slot.
                    // If this implementation is ever used on such a platform, this if block should be
                    // wrapped in a finally / prepared region.
                    if (Interlocked.CompareExchange(ref headAndTail.Tail, currentTail + 1, currentTail) == currentTail)
                    {
                        // Successfully reserved the slot.  Note that after the above CompareExchange, other threads
                        // trying to return will end up spinning until we do the subsequent Write.
                        slots[slotsIndex].Item = item;
                        Volatile.Write(ref slots[slotsIndex].SequenceNumber, currentTail + 1);
                        return true;
                    }
                }
                else if (diff < 0)
                {
                    // The sequence number was less than what we needed, which means this slot still
                    // contains a value, i.e. the segment is full.  Technically it's possible that multiple
                    // dequeuers could have read concurrently, with those getting later slots actually
                    // finishing first, so there could be spaces after this one that are available, but
                    // we need to enqueue in order.
                    return false;
                }

                // Lost a race. Spin a bit, then try again.
                spinner.SpinOnce();
            }
        }

        public void Clear()
        {
            // TODO: re-allocate the slot buffer
            for (var i = 0; i < slots.Length; i++)
            {
                if (!TryTake(out var _))
                {
                    break;
                }
            }
        }

        [StructLayout(LayoutKind.Auto)]
        [DebuggerDisplay("Item = {Item}, SequenceNumber = {SequenceNumber}")]
        internal struct Slot
        {
            /// <summary>The item.</summary>
            public T Item;
            /// <summary>The sequence number for this slot, used to synchronize between enqueuers and dequeuers.</summary>
            public int SequenceNumber;
        }
    }

    [DebuggerDisplay("Head = {Head}, Tail = {Tail}")]
    [StructLayout(LayoutKind.Explicit, Size = 3 * Padding.CACHE_LINE_SIZE)] // padding before/between/after fields
    internal struct PaddedHeadAndTail
    {
        [FieldOffset(1 * Padding.CACHE_LINE_SIZE)] public int Head;
        [FieldOffset(2 * Padding.CACHE_LINE_SIZE)] public int Tail;
    }
}