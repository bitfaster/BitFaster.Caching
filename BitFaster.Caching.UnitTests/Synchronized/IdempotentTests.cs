﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BitFaster.Caching.Synchronized;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Synchronized
{
    public class IdempotentTests
    {
        [Fact]
        public void DefaultCtorValueIsNotCreated()
        {
            var a = new Idempotent<int, int>();

            a.IsValueCreated.Should().BeFalse();
            a.ValueIfCreated.Should().Be(0);
        }

        [Fact]
        public void WhenValuePassedToCtorValueIsStored()
        {
            var a = new Idempotent<int, int>(1);

            a.ValueIfCreated.Should().Be(1);
            a.IsValueCreated.Should().BeTrue();
        }

        [Fact]
        public void WhenValueCreatedValueReturned()
        {
            var a = new Idempotent<int, int>();
            a.GetValue(1, k => 2).Should().Be(2);

            a.ValueIfCreated.Should().Be(2);
            a.IsValueCreated.Should().BeTrue();
        }

        [Fact]
        public void WhenValueCreatedGetValueReturnsOriginalValue()
        {
            var a = new Idempotent<int, int>();
            a.GetValue(1, k => 2);
            a.GetValue(1, k => 3).Should().Be(2);
        }

        [Fact]
        public async Task WhenCallersRunConcurrentlyResultIsFromWinner()
        {
            var enter = new ManualResetEvent(false);
            var resume = new ManualResetEvent(false);

            var idempotent = new Idempotent<int, int>();
            var result = 0;
            var winnerCount = 0;

            Task<int> first = Task.Run(() =>
            {
                return idempotent.GetValue(1, k =>
                {
                    enter.Set();
                    resume.WaitOne();

                    result = 1;
                    Interlocked.Increment(ref winnerCount);
                    return 1;
                });
            });

            Task<int> second = Task.Run(() =>
            {
                return idempotent.GetValue(1, k =>
                {
                    enter.Set();
                    resume.WaitOne();

                    result = 2;
                    Interlocked.Increment(ref winnerCount);
                    return 2;
                });
            });

            enter.WaitOne();
            resume.Set();

            (await first).Should().Be(result);
            (await second).Should().Be(result);

            winnerCount.Should().Be(1);
        }
    }
}
