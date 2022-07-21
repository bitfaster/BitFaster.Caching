using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests
{
    public class AtomTests
    {
        [Fact]
        public void DefaultCtorValueIsNotCreated()
        {
            var a = new Atom<int, int>();

            a.IsValueCreated.Should().BeFalse();
            a.ValueIfCreated.Should().Be(0);
        }

        [Fact]
        public void WhenValuePassedToCtorValueIsStored()
        {
            var a = new Atom<int, int>(1);

            a.ValueIfCreated.Should().Be(1);
            a.IsValueCreated.Should().BeTrue();
        }

        [Fact]
        public void WhenValueCreatedValueReturned()
        {
            var a = new Atom<int, int>();
            a.GetValue(1, k => 2).Should().Be(2);

            a.ValueIfCreated.Should().Be(2);
            a.IsValueCreated.Should().BeTrue();
        }

        [Fact]
        public void WhenValueCreatedGetValueReturnsOriginalValue()
        {
            var a = new Atom<int, int>();
            a.GetValue(1, k => 2);
            a.GetValue(1, k => 3).Should().Be(2);
        }

        [Fact]
        public async Task WhenCallersRunConcurrentlyResultIsFromWinner()
        {
            var enter = new ManualResetEvent(false);
            var resume = new ManualResetEvent(false);

            var atom = new Atom<int, int>();
            int result = 0;
            int winners = 0;

            Task<int> first = Task.Run(() =>
            {
                return atom.GetValue(1, k =>
                {
                    enter.Set();
                    resume.WaitOne();

                    result = 1;
                    Interlocked.Increment(ref winners);
                    return 1;
                });
            });

            Task<int> second = Task.Run(() =>
            {
                return atom.GetValue(1, k =>
                {
                    enter.Set();
                    resume.WaitOne();

                    result = 2;
                    Interlocked.Increment(ref winners);
                    return 2;
                });
            });

            enter.WaitOne();
            resume.Set();

            (await first).Should().Be(result);
            (await second).Should().Be(result);

            winners.Should().Be(1);
        }
    }
}
