using System;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests
{
    public class TypePropsTests
    {
        private static readonly MethodInfo method = typeof(TypePropsTests).GetMethod(nameof(TypePropsTests.IsWriteAtomic), BindingFlags.NonPublic | BindingFlags.Static);

        [Theory]
        [InlineData(typeof(object), true)]
        [InlineData(typeof(IntPtr), true)]
        [InlineData(typeof(UIntPtr), true)]
        [InlineData(typeof(int), true)]
        [InlineData(typeof(long), true)] // this is only expected to pass on 64bit platforms
        [InlineData(typeof(Guid), false)]
        public void Test(Type argType, bool expected)
        {
            var isWriteAtomic = method.MakeGenericMethod(argType);

            isWriteAtomic.Invoke(null, null).Should().BeOfType<bool>().Which.Should().Be(expected);
        }

        private static bool IsWriteAtomic<T>()
        {
            return TypeProps<T>.IsWriteAtomic;
        }
    }
}
