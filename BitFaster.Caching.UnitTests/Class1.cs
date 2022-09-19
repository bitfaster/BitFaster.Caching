using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace BitFaster.Caching.UnitTests
{
    public class Class1
    {
        [Fact]
        public void ArmTest()
        {
            if (AdvSimd.IsSupported)
            {
                throw new Exception("AdvSimd!");
            }
        }
    }
}
