using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.FSharp.Core;
using Plotly.NET;
using Plotly.NET.CSharp;
using Plotly.NET.LayoutObjects;

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

        public static GenericChart.GenericChart WithoutVerticalGridlines(this GenericChart.GenericChart chart)
        {
            var axis = LinearAxis.init<IConvertible, IConvertible, IConvertible, IConvertible, IConvertible, IConvertible>(ShowGrid: new FSharpOption<bool>(false));
            return chart.WithXAxis(axis);
        }

        public static GenericChart.GenericChart WithLayout(this GenericChart.GenericChart chart, string title)
        {
            FSharpOption<Title> t = Title.init(Text: title, X: 0.5);
            FSharpOption <Color> plotBGColor = new FSharpOption<Color>(Color.fromKeyword(Plotly.NET.ColorKeyword.WhiteSmoke));
            Layout layout = Layout.init<IConvertible>(PlotBGColor: plotBGColor, Title: t);
            return chart.WithLayout(layout);
        }
    }
}
