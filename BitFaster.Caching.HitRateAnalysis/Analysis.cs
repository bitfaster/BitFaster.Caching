using System;
using System.Collections.Generic;
using System.Text;

namespace BitFaster.Caching.HitRateAnalysis
{
    public class Analysis
    {
        public int N { get; set; }

        public double s { get; set; }

        public int Samples { get; set; }

        public double CacheSizePercent { get; set; }

        public void WriteSummaryToConsole()
        {
            Console.WriteLine($"Analyzing with N={N}, s={s}, Samples={Samples}, Cache Size ={CacheSizePercent*100.0}%");
        }
    }
}
