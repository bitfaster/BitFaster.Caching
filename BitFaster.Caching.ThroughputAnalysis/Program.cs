using System;
using System.Threading;
using BitFaster.Caching.ThroughputAnalysis;

Host.PrintInfo();

int minWorker, minIOC;
ThreadPool.GetMinThreads(out minWorker, out minIOC);
ThreadPool.SetMinThreads(Environment.ProcessorCount*2, minIOC);

var (mode, size) = CommandParser.Parse(args);

Runner.Run(mode, size);
Console.WriteLine("Done.");
