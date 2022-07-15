using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ConsoleTables;
using CsvHelper;

namespace BitFaster.Caching.HitRateAnalysis.Zipfian
{
    public class AnalysisResult
    {
        public string Cache { get; set; }

        public int N { get; set; }

        public double s { get; set; }

        public int Samples { get; set; }

        public bool IsScan { get; set; }

        public double CacheSizePercent { get; set; }

        public double HitRatio { get; set; }

        public TimeSpan Duration { get; set; }

        public static void WriteToFile(string path, IEnumerable<AnalysisResult> results)
        {
            using (var writer = new StreamWriter(path))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(results);
            }
        }
    }
}
