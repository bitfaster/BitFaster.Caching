﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Scheduler
{
    /// <summary>
    /// Represents a scheduler that runs tasks synchronously.
    /// </summary>
    public class ForegroundScheduler : IScheduler
    {
        private long count;

        public bool IsBackground => false;

        public long RunCount => count;

        public Optional<Exception> LastException => Optional<Exception>.None();

        public void Run(Action action)
        {
            count++;
            action();
        }
    }
}