using System;
using System.Threading.Tasks;

var menu = new EasyConsole.Menu()
    .Add("Zipf", () => BitFaster.Caching.HitRateAnalysis.Zipfian.Runner.Run())
    .Add("Wikibench", () => BitFaster.Caching.HitRateAnalysis.Wikibench.Runner.Run().Wait());

menu.Display();
