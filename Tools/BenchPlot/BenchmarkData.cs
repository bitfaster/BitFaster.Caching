using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BenchPlot
{
    public class BenchmarkData
    {
        public string Method { get; set; }

        public string Job { get; set; }

        public string Mean { get; set; }

        public string StdDev { get; set; }

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
