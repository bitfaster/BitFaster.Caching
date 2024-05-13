﻿using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace BitFaster.Caching.UnitTests.Retry
{
    [Serializable]
    public class RetryTestCase : XunitTestCase
    {
        private int maxRetries;

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Called by the de-serializer; should only be called by deriving classes for de-serialization purposes")]
        public RetryTestCase() { }

        public RetryTestCase(IMessageSink diagnosticMessageSink, TestMethodDisplay testMethodDisplay, TestMethodDisplayOptions defaultMethodDisplayOptions, ITestMethod testMethod, int maxRetries, object[] testMethodArguments = null)
            : base(diagnosticMessageSink, testMethodDisplay, defaultMethodDisplayOptions, testMethod, testMethodArguments: testMethodArguments)
        {
            this.maxRetries = maxRetries;
        }

        // This method is called by the xUnit test framework classes to run the test case. We will do the
        // loop here, forwarding on to the implementation in XunitTestCase to do the heavy lifting. We will
        // continue to re-run the test until the aggregator has an error (meaning that some internal error
        // condition happened), or the test runs without failure, or we've hit the maximum number of tries.
        public override async Task<RunSummary> RunAsync(IMessageSink diagnosticMessageSink,
                                                        IMessageBus messageBus,
                                                        object[] constructorArguments,
                                                        ExceptionAggregator aggregator,
                                                        CancellationTokenSource cancellationTokenSource)
        {
            var runCount = 0;

            while (true)
            {
                // This is really the only tricky bit: we need to capture and delay messages (since those will
                // contain run status) until we know we've decided to accept the final result;
                var delayedMessageBus = new DelayedMessageBus(messageBus);

                var summary = await base.RunAsync(diagnosticMessageSink, delayedMessageBus, constructorArguments, aggregator, cancellationTokenSource);
                if (aggregator.HasExceptions || summary.Failed == 0 || ++runCount >= maxRetries)
                {
                    delayedMessageBus.Dispose();  // Sends all the delayed messages
                    return summary;
                }

                diagnosticMessageSink.OnMessage(new DiagnosticMessage("Execution of '{0}' failed (attempt #{1}), retrying...", DisplayName, runCount));
            }
        }

        public override void Serialize(IXunitSerializationInfo data)
        {
            base.Serialize(data);

            data.AddValue("MaxRetries", maxRetries);
        }

        public override void Deserialize(IXunitSerializationInfo data)
        {
            base.Deserialize(data);

            maxRetries = data.GetValue<int>("MaxRetries");
        }
    }
}
