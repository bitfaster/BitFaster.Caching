using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ConsoleTables;

namespace BitFaster.Caching.HitRateAnalysis
{
    public static class ResultExtensions
    {
        public static void WriteToConsole<T>(this IEnumerable<T> results)
        {
            ConsoleTable
                .From(results)
                .Configure(o => o.NumberAlignment = Alignment.Right)
                .Write(Format.MarkDown);
        }
    }
}
