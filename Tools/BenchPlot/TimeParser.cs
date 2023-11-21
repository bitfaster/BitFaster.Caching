using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BenchPlot
{
    public static class TimeParser
    {
        public static double Parse(string time)
        {
            var split = time.Split(' ');

            if (time.EndsWith("μs"))
            {
                return double.Parse(split[0]) * 0.001;
            }

            if (time.EndsWith("ns"))
            {
                return double.Parse(split[0]);
            }

            if (time.EndsWith("ms"))
            {
                return double.Parse(split[0]) * 1_000_000;
            }

            if (time.EndsWith("NA"))
            {
                return 0;
            }

            if (time.EndsWith("?"))
            {
                return 0;
            }

            throw new InvalidOperationException();
        }
    }
}
