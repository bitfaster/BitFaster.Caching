using Microsoft.FSharp.Core;
using Plotly.NET.LayoutObjects;
using Plotly.NET;

namespace BenchPlot
{
    public static class PlotExt
    {
        public static GenericChart.GenericChart WithAxisTitles(this GenericChart.GenericChart chart, string yTitle)
        {
            FSharpOption<string> yt = new FSharpOption<string>(yTitle);
            return chart.WithYAxisStyle(Title.init(yt));
        }

        public static GenericChart.GenericChart WithoutVerticalGridlines(this GenericChart.GenericChart chart)
        {
            var axis = LinearAxis.init<IConvertible, IConvertible, IConvertible, IConvertible, IConvertible, IConvertible>(ShowGrid: new FSharpOption<bool>(false));
            return chart.WithXAxis(axis);
        }

        public static GenericChart.GenericChart WithLayout(this GenericChart.GenericChart chart, string title)
        {
            FSharpOption<Title> t = Title.init(Text: title, X: 0.5);
            FSharpOption<Color> plotBGColor = new FSharpOption<Color>(Color.fromKeyword(Plotly.NET.ColorKeyword.WhiteSmoke));
            Layout layout = Layout.init<IConvertible>(PlotBGColor: plotBGColor, Title: t);
            return chart.WithLayout(layout);
        }
    }
}
