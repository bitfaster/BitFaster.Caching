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
        private readonly string title;
        private readonly List<Analysis<long>> analysis;
        private readonly ArcDataFile file;

        public RunnerConfig(string name, string title, int[] cacheSizes, Uri dataUri)
        {
            this.name = name;
            this.title = title;
            this.analysis = cacheSizes.Select(s => new Analysis<long>(s)).ToList();
            this.file = new ArcDataFile(dataUri);
        }

        public string Name => this.name;

        public string Title => this.title;

        public IEnumerable<Analysis<long>> Analysis => this.analysis;

        public ArcDataFile File => this.file;

        public static RunnerConfig Database = new RunnerConfig("results.arc.database.csv", "Arc Database", new[] { 1_000_000, 2_000_000, 3_000_000, 4_000_000, 5_000_000, 6_000_000, 7_000_000, 8_000_000 }, new Uri("https://github.com/bitfaster/cache-datasets/releases/download/v1.0/DS1.lis.gz"));
        public static RunnerConfig Search = new RunnerConfig("results.arc.search.csv", "Arc Search (S3)", new[] { 100_000, 200_000, 300_000, 400_000, 500_000, 600_000, 700_000, 800_000 }, new Uri("https://github.com/bitfaster/cache-datasets/releases/download/v1.0/S3.lis.gz"));
        public static RunnerConfig Oltp = new RunnerConfig("results.arc.oltp.csv", "Arc OLTP", new[] { 250, 500, 750, 1000, 1250, 1500, 1750, 2000 }, new Uri("https://github.com/bitfaster/cache-datasets/releases/download/v1.0/OLTP.lis.gz"));
    }
}
