using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace BitFaster.Caching.UnitTests.Retry
{
    public class DelayedMessageBus : IMessageBus
    {
        private readonly IMessageBus innerBus;
        private readonly List<IMessageSinkMessage> messages = new List<IMessageSinkMessage>();

        public DelayedMessageBus(IMessageBus innerBus)
        {
            this.innerBus = innerBus;
        }

        public bool QueueMessage(IMessageSinkMessage message)
        {
            // Technically speaking, this lock isn't necessary in our case, because we know we're using this
            // message bus for a single test (so there's no possibility of parallelism). However, it's good
            // practice when something might be used where parallel messages might arrive, so it's here in
            // this sample.
            lock (messages)
                messages.Add(message);

            // No way to ask the inner bus if they want to cancel without sending them the message, so
            // we just go ahead and continue always.
            return true;
        }

        public void Dispose()
        {
            foreach (var message in messages)
                innerBus.QueueMessage(message);
        }
    }
}
