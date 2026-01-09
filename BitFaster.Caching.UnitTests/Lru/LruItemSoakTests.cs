using System;
using System.Threading;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lru
{
    [Collection("Soak")]
    public class LruItemSoakTests
    {
        private const int soakIterations = 3;
        private readonly LruItem<int, MassiveStruct> item = new(1, MassiveStruct.A);

        // Adapted from
        // https://stackoverflow.com/questions/23262513/reproduce-torn-reads-of-decimal-in-c-sharp
        [Theory]
        [Repeat(soakIterations)]
        public async Task DetectTornStruct(int _)
        {
            using var source = new CancellationTokenSource();
            var started = new TaskCompletionSource<bool>();

            var setTask = Task.Run(() => Setter(source.Token, started));
            await started.Task;
            Checker(source);

            await setTask;
        }

        private void Setter(CancellationToken cancelToken, TaskCompletionSource<bool> started)
        {
            started.SetResult(true);

            while (true)
            {
                item.SeqLockWrite(MassiveStruct.A);
                item.SeqLockWrite(MassiveStruct.B);

                if (cancelToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }

        private void Checker(CancellationTokenSource source)
        {
            // On my machine, without SeqLock, this consistently fails below 100 iterations
            // on debug build, and below 1000 on release build
            for (int count = 0; count < 10_000; ++count)
            {
                var t = item.SeqLockRead();

                if (t != MassiveStruct.A && t != MassiveStruct.B)
                {
                    throw new Exception($"Value is torn after {count} iterations");
                }
            }

            source.Cancel();
        }

#pragma warning disable CS0659 // Object.Equals but no GetHashCode
#pragma warning disable CS0661 // operator== but no GetHashCode     
        public struct MassiveStruct : IEquatable<MassiveStruct>
        {
            // To repro on x64, struct should be larger than a cache line (64 bytes).
            public long a;
            public long b;
            public long c;
            public long d;

            public long e;
            public long f;
            public long g;
            public long h;

            public long i;

            public static readonly MassiveStruct A = new MassiveStruct();
            public static readonly MassiveStruct B = new MassiveStruct()
            { a = long.MaxValue, b = long.MaxValue, c = long.MaxValue, d = long.MaxValue, e = long.MaxValue, f = long.MaxValue, g = long.MaxValue, h = long.MaxValue, i = long.MaxValue };

            public override bool Equals(object obj)
            {
                return obj is MassiveStruct @struct && Equals(@struct);
            }

            public bool Equals(MassiveStruct other)
            {
                return a == other.a &&
                       b == other.b &&
                       c == other.c &&
                       d == other.d &&
                       e == other.e &&
                       f == other.f &&
                       g == other.g &&
                       h == other.h &&
                       i == other.i;
            }

            public static bool operator ==(MassiveStruct left, MassiveStruct right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(MassiveStruct left, MassiveStruct right)
            {
                return !(left == right);
            }
        }
#pragma warning restore CS0659
#pragma warning restore CS0661
    }
}
