using System;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests
{
    public class TypePropsTests
    {
        [Theory]
        [InlineData(typeof(object), true)]
        [InlineData(typeof(IntPtr), true)]
        [InlineData(typeof(UIntPtr), true)]
        [InlineData(typeof(int), true)]
        [InlineData(typeof(Guid), false)]
        public void Test(Type argType, bool expected)
        { 
            MethodInfo method = typeof(TypePropsTests).GetMethod(nameof(TypePropsTests.IsWriteAtomic), BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo generic = method.MakeGenericMethod(argType);

            generic.Invoke(null, null).Should().BeOfType<bool>().Which.Should().Be(expected);
        }

        private static bool IsWriteAtomic<T>()
        { 
            return TypeProps<T>.IsWriteAtomic;
        }
    }
}
