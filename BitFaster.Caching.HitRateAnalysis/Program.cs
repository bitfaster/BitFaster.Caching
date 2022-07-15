using System;
using System.Threading.Tasks;
using BitFaster.Caching.HitRateAnalysis.Arc;

var menu = new EasyConsole.Menu()
    .Add("Zipf", () => BitFaster.Caching.HitRateAnalysis.Zipfian.Runner.Run())
    .Add("Wikibench", () => BitFaster.Caching.HitRateAnalysis.Wikibench.Runner.Run().Wait())
    .Add("Glimpse", () => BitFaster.Caching.HitRateAnalysis.Glimpse.Runner.Run().Wait())
    .Add("Arc Database", () => new BitFaster.Caching.HitRateAnalysis.Arc.Runner(RunnerConfig.Database).Run().Wait())
    .Add("Arc Search", () => new BitFaster.Caching.HitRateAnalysis.Arc.Runner(RunnerConfig.Search).Run().Wait())
    .Add("Arc OLTP", () => new BitFaster.Caching.HitRateAnalysis.Arc.Runner(RunnerConfig.Oltp).Run().Wait());

menu.Display();
