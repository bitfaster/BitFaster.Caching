using System;
using System.Threading;
using System.Threading.Tasks;
using BitFaster.Caching.Lfu;
using Xunit;
using static BitFaster.Caching.UnitTests.Lru.LruItemSoakTests;

namespace BitFaster.Caching.UnitTests.Lfu
{
    [Collection("Soak")]
    public class LfuNodeSoakTest
    {
        private const int soakIterations = 3;
        private readonly LfuNode<int, MassiveStruct> item = new(1, MassiveStruct.A);

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
    }
}
