using Xunit;
using Xunit.Sdk;

namespace BitFaster.Caching.UnitTests.Retry
{
    [XunitTestCaseDiscoverer("BitFaster.Caching.UnitTests.Retry.RetryFactDiscoverer", "BitFaster.Caching.UnitTests")]
    public class RetryFactAttribute : FactAttribute
    {
        /// <summary>
        /// Number of retries allowed for a failed test. If unset (or set less than 1), will
        /// default to 3 attempts.
        /// </summary>
        public int MaxRetries { get; set; }
    }
}
