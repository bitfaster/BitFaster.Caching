using Xunit.Sdk;
using Xunit;

namespace BitFaster.Caching.UnitTests.Retry
{
    [XunitTestCaseDiscoverer("RetryFactExample.RetryFactDiscoverer", "RetryFactExample")]
    public class RetryFactAttribute : FactAttribute
    {
        /// <summary>
        /// Number of retries allowed for a failed test. If unset (or set less than 1), will
        /// default to 3 attempts.
        /// </summary>
        public int MaxRetries { get; set; }
    }
}
