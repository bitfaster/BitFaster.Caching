using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ConsoleTables;
using CsvHelper;

namespace BitFaster.Caching.HitRateAnalysis
{
    // Cache         | cache size % |   s   |  Hit Ratio 
    //---------------+--------------+-------+------------
    // ClassicLru    |    5 %       | 0.86  |    20%
    // ClassicLru    |    10 %      | 0.86  |    20%
    // ClassicLru    |    20 %      | 0.86  |    20%
    // ClassicLru    |    30 %      | 0.86  |    20%
    // ClassicLru    |    40 %      | 0.86  |    20%
    // ConcurrentLru |    5 %       | 0.86  |    20%
    // etc.          |    10 %      | 0.86  |    20%

    public class AnalysisResult
    {
        public string Cache { get; set; }

        public int N { get; set; }

        public double s { get; set; }

        public int Samples { get; set; }

        public double CacheSizePercent { get; set; }

        public double HitRatio { get; set; }

        public static void WriteToFile(string path, IEnumerable<AnalysisResult> results)
        {
            using (var writer = new StreamWriter(path))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(results);
            }
        }

        public static void WriteToConsole(IEnumerable<AnalysisResult> results)
        {
            ConsoleTable
                .From<AnalysisResult>(results)
                .Configure(o => o.NumberAlignment = Alignment.Right)
                .Write(Format.Alternative);
        }
    }
}
