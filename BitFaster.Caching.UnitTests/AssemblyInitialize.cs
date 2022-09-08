using System.Threading;
using Xunit.Abstractions;
using Xunit.Sdk;

[assembly: Xunit.TestFramework("BitFaster.Caching.UnitTests.AssemblyInitialize", "BitFaster.Caching.UnitTests")]

namespace BitFaster.Caching.UnitTests
{
    public class AssemblyInitialize : XunitTestFramework
    {
        public AssemblyInitialize(IMessageSink messageSink)
            : base(messageSink)
        {
            ThreadPool.SetMinThreads(16, 16);
        }

        public new void Dispose()
        {
            // Place tear down code here
            base.Dispose();
        }
    }
}
