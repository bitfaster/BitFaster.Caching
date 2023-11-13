using System;
using System.Diagnostics;
using System.Threading;
using FluentAssertions;

namespace BitFaster.Caching.UnitTests
{
    // Helper class for running timed integration tests. This is useful to verify
    // correct expiry behavior in an end to end integration test with real code.
    // This class uses Thread.Sleep instead of async/await because xunit will 
    // aggressively run tests on any await yielding thread making timing unpredictable.
    public class Timed
    {
        public static void Execute<TArg, TState>(TArg arg, Func<TArg, TState> first, TimeSpan pause, Action<TState> second)
        {
            int attempts = 0;
            while (true)
            {
                var sw = Stopwatch.StartNew();

                var state = first(arg);
                Thread.Sleep(pause);

                if (sw.Elapsed < pause + TimeSpan.FromMilliseconds(25))
                {
                    second(state);
                    return;
                }

                Thread.Sleep(200);
                attempts++.Should().BeLessThan(128, "Unable to run test within verification margin");
            }
        }

        public static void Execute<TArg, TState>(TArg arg, Func<TArg, TState> first, TimeSpan pause1, Action<TState> second, TimeSpan pause2, Action<TState> third)
        {
            int attempts = 0;
            while (true)
            {
                var sw = Stopwatch.StartNew();

                var state = first(arg);
                Thread.Sleep(pause1);

                if (sw.Elapsed < pause1 + TimeSpan.FromMilliseconds(25))
                {
                    second(state);
                    Thread.Sleep(pause2);

                    if (sw.Elapsed < pause1 + pause2 + TimeSpan.FromMilliseconds(25))
                    {
                        third(state);
                        return;
                    }    
                }

                Thread.Sleep(200);
                attempts++.Should().BeLessThan(128, "Unable to run test within verification margin");
            }
        }
    }
}
