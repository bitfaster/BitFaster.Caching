using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using Xunit;
using FluentAssertions;

namespace BitFaster.Caching.UnitTests
{
    public class SynchronizedTests
    {
        private int target = 42;
        private bool initialized = true;
        private object syncLock = new object();

        [Fact]
        public void WhenIsIntializedValueParamIsNotUsed()
        {
            Synchronized.Initialize(ref target, ref initialized, ref syncLock, 666).Should().Be(42);
        }

        [Fact]
        public void WhenIsIntializedValueFactoryIsNotUsed()
        {
            Synchronized.Initialize(ref target, ref initialized, ref syncLock, k => 666, 2).Should().Be(42);
        }
    }
}
