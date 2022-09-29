using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using BitFaster.Caching.ThroughputAnalysis;
using Iced.Intel;
using MathNet.Numerics.Distributions;

Host.PrintInfo();

var (mode, size) = CommandParser.Parse(args);

Runner.Run(mode, size);
Console.WriteLine("Done.");
