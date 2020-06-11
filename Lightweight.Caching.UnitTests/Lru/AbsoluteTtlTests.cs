using Lightweight.Caching.Lru;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Lightweight.Caching.UnitTests.Lru
{
    public class AbsoluteTtlTests
    {
        [Fact]
        public void Blah()
        {
            var policy = new AbsoluteTtl<int, int>(TimeSpan.FromSeconds(1));

            var i = new TimeStampedLruItem<int, int>(1, 1);

            policy.ShouldDiscard(i);
        }
    }
}
