using CsvHelper.Configuration;

namespace BenchPlot
{
    public class BenchmarkData
    {
        public string Method { get; set; } = string.Empty;

        public string Job { get; set; } = string.Empty;

        public string Mean { get; set; } = string.Empty;

        public string StdDev { get; set; } = string.Empty;

        //public string Size { get; set; }
    }

    public class BenchMap : ClassMap<BenchmarkData>
    {
        public BenchMap()
        {
            Map(m => m.Method).Name("Method");
            Map(m => m.Job).Name("Job");
            Map(m => m.Mean).Name("Mean");
            Map(m => m.StdDev).Name("StdDev");
            //Map(m => m.Size).Name("Size");
        }
    }
}
