using System;
using BitFaster.Caching.ThroughputAnalysis;

Host.PrintInfo();

Mode mode = Mode.Read;

var menu = new EasyConsole.Menu()
    .Add("Read", () => mode = Mode.Read)
    .Add("Read + Write", () => mode = Mode.ReadWrite)
    .Add("Update", () => mode = Mode.Update)
    .Add("Evict", () => mode = Mode.Evict)
    .Add("All", () => mode = Mode.All);

menu.Display();
Runner.Run(mode);
Console.WriteLine("Done.");
