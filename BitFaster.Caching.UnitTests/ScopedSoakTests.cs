using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests
{
    [Collection("Soak")]
    public class ScopedSoakTests
    {
        [Fact]
        public async Task WhenSoakCreateLifetimeScopeIsDisposedCorrectly()
        {
            for (int i = 0; i < 10; i++)
            {
                var scope = new Scoped<Disposable>(new Disposable(i));

                await Threaded.Run(4, () => {
                    for (int i = 0; i < 100000; i++)
                    {
                        using (var l = scope.CreateLifetime())
                        {
                            l.Value.IsDisposed.Should().BeFalse();
                        }
                    }
                });

                scope.IsDisposed.Should().BeFalse();
                scope.Dispose();
                scope.TryCreateLifetime(out _).Should().BeFalse();
                scope.IsDisposed.Should().BeTrue();
            }
        }
    }
}
