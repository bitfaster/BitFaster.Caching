using System;
using Microsoft.FSharp.Core;
using Plotly.NET;
using Plotly.NET.CSharp;
using Plotly.NET.LayoutObjects;

namespace BitFaster.Caching.HitRateAnalysis
{
    public static class PlotExt
    {
        public static GenericChart WithAxisTitles(this GenericChart chart, string xTitle, string yTitle)
        {
            var font = new FSharpOption<Font>(Font.init(Size: new FSharpOption<double>(16)));
            FSharpOption<string> xt = new FSharpOption<string>(xTitle);
            FSharpOption<string> yt = new FSharpOption<string>(yTitle);
            return chart.WithXAxisStyle(Title.init(xt, Font: font)).WithYAxisStyle(Title.init(yt, Font: font));
        }

        public static GenericChart WithoutVerticalGridlines(this GenericChart chart)
        {
            var gridColor = new FSharpOption<Color>(Color.fromKeyword(ColorKeyword.Gainsboro));
            var yaxis = LinearAxis.init<IConvertible, IConvertible, IConvertible, IConvertible, IConvertible, IConvertible, IConvertible, IConvertible>(
                GridColor: gridColor,
                ZeroLineColor: gridColor);

            var axis = LinearAxis.init<IConvertible, IConvertible, IConvertible, IConvertible, IConvertible, IConvertible, IConvertible, IConvertible>(ShowGrid: new FSharpOption<bool>(false));
            return chart.WithXAxis(axis).WithYAxis(yaxis);
        }

        public static GenericChart WithLayout(this GenericChart chart, string title)
        {
            var font = new FSharpOption<Font>(Font.init(Size: new FSharpOption<double>(24)));
            FSharpOption<Title> t = Title.init(Text: title, X: 0.5, Font: font);
            FSharpOption<Color> plotBGColor = new FSharpOption<Color>(Color.fromKeyword(ColorKeyword.WhiteSmoke));
            Layout layout = Layout.init<IConvertible>(PaperBGColor: plotBGColor, PlotBGColor: plotBGColor, Title: t);
            return chart.WithLayout(layout);
        }
    }
}
