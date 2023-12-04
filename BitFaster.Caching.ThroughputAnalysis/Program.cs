using System;
using BitFaster.Caching.ThroughputAnalysis;

Host.PrintInfo();
PowerPlan.EnableHighPerformance();
Console.WriteLine();

var (mode, size) = CommandParser.Parse(args);

Runner.Run(mode, size);
Console.WriteLine("Done.");
