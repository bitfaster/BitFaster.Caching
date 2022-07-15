using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.HitRateAnalysis.Arc
{
    public class RunnerConfig
    {
        private readonly string name;
        private readonly List<Analysis> analysis;
        private readonly ArcDataFile file;

        public RunnerConfig(string name, int[] cacheSizes, Uri dataUri)
        {
            this.name = name;
            this.analysis = cacheSizes.Select(s => new Analysis(s)).ToList();
            this.file = new ArcDataFile(dataUri);
        }

        public string Name => this.name;

        public IEnumerable<Analysis> Analysis => this.analysis;

        public ArcDataFile File => this.file;

        public static RunnerConfig Database = new RunnerConfig("results.arc.database.csv", new[] { 100000, 200000, 300000, 400000, 500000, 600000, 700000, 800000 }, new Uri("https://github.com/bitfaster/cache-datasets/releases/download/v1.0/DS1.lis.gz"));
        public static RunnerConfig Search = new RunnerConfig("results.arc.search.csv", new[] { 100000, 200000, 300000, 400000, 500000, 600000, 700000, 800000 }, new Uri("https://github.com/bitfaster/cache-datasets/releases/download/v1.0/S1.lis.gz"));
        public static RunnerConfig Oltp = new RunnerConfig("results.arc.oltp.csv", new[] { 250, 500, 750, 1000, 1250, 1500, 1750, 2000 }, new Uri("https://github.com/bitfaster/cache-datasets/releases/download/v1.0/OLTP.lis.gz"));
    }
}
