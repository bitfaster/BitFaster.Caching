using Microsoft.FSharp.Core;
using Plotly.NET.LayoutObjects;
using Plotly.NET;

namespace BenchPlot
{
    public static class PlotExt
    {
        public static GenericChart.GenericChart WithAxisTitles(this GenericChart.GenericChart chart, string yTitle)
        {
            var font = new FSharpOption<Font>(Font.init(Size: new FSharpOption<double>(16)));
            FSharpOption<string> yt = new FSharpOption<string>(yTitle);
            return chart.WithXAxisStyle(Title.init(Font: font)).WithYAxisStyle(Title.init(yt, Font: font));
        }

        public static GenericChart.GenericChart WithoutVerticalGridlines(this GenericChart.GenericChart chart)
        {
            var gridColor = new FSharpOption<Color>(Color.fromKeyword(ColorKeyword.Gainsboro));
            var yaxis = LinearAxis.init<IConvertible, IConvertible, IConvertible, IConvertible, IConvertible, IConvertible>(
                GridColor: gridColor,
                ZeroLineColor: gridColor);

            var axis = LinearAxis.init<IConvertible, IConvertible, IConvertible, IConvertible, IConvertible, IConvertible>(ShowGrid: new FSharpOption<bool>(false));
            return chart.WithXAxis(axis).WithYAxis(yaxis);
        }

        public static GenericChart.GenericChart WithLayout(this GenericChart.GenericChart chart, string title)
        {
            var font = new FSharpOption<Font>(Font.init(Size: new FSharpOption<double>(24)));
            FSharpOption<Title> t = Title.init(Text: title, X: 0.5, Font: font);
            FSharpOption<Color> plotBGColor = new FSharpOption<Color>(Color.fromKeyword(ColorKeyword.WhiteSmoke));
            Layout layout = Layout.init<IConvertible>(PaperBGColor: plotBGColor, PlotBGColor: plotBGColor, Title: t);
            return chart.WithLayout(layout);
        }
    }
}
