using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.FSharp.Core;
using Plotly.NET;
using Plotly.NET.CSharp;

namespace BitFaster.Caching.HitRateAnalysis
{
    public static class PlotExt
    {
        public static GenericChart.GenericChart WithAxisTitles(this GenericChart.GenericChart chart, string xTitle, string yTitle)
        {
            FSharpOption<string> xt = new FSharpOption<string>(xTitle);
            FSharpOption<string> yt = new FSharpOption<string>(yTitle);
            return chart.WithXAxisStyle(Title.init(xt)).WithYAxisStyle(Title.init(yt));
        }
    }
}
