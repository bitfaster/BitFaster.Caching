using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using CsvHelper;
using Microsoft.FSharp.Core;
using Plotly.NET;
using Plotly.NET.ImageExport;
using Plotly.NET.LayoutObjects;

namespace BitFaster.Caching.ThroughputAnalysis
{
    public class Exporter
    {
        DataTable resultTable = new DataTable();

        public Exporter(int minThreads, int maxThreads)
        {
            // output:
            // ThreadCount   1  2  3  4  5
            // Classic       5  6  7  7  8
            // Concurrent    5  6  7  7  8

            resultTable.Clear();
            resultTable.Columns.Add("ThreadCount");
            foreach (var tc in Enumerable.Range(minThreads, maxThreads - (minThreads-1)).ToArray())
            {
                resultTable.Columns.Add(tc.ToString());
            }
        }

        public void Initialize(IEnumerable<ICacheFactory> caches)
        {
            foreach (var c in caches)
            {
                c.DataRow = resultTable.NewRow();
                c.DataRow["ThreadCount"] = c.Name;
            }
        }

        public void CaptureRows(IEnumerable<ICacheFactory> caches)
        {
            foreach (var c in caches)
            {
                resultTable.Rows.Add(c.DataRow);
            }
        }

        public void ExportCsv(Mode mode, int cacheSize)
        {
            using (var textWriter = File.CreateText($"Results_{mode}_{cacheSize}.csv"))
            using (var csv = new CsvWriter(textWriter, CultureInfo.InvariantCulture))
            {
                foreach (DataColumn column in resultTable.Columns)
                {
                    csv.WriteField(column.ColumnName);
                }
                csv.NextRecord();

                foreach (DataRow row in resultTable.Rows)
                {
                    for (var i = 0; i < resultTable.Columns.Count; i++)
                    {
                        csv.WriteField(row[i]);
                    }
                    csv.NextRecord();
                }
            }
        }

        public void ExportPlot(Mode mode, int cacheSize)
        {
            var columns = new List<int>();

            for(int i = 1; i < resultTable.Columns.Count; i++)
            {
                columns.Add(int.Parse(resultTable.Columns[i].ColumnName));
            }

            List<GenericChart.GenericChart> charts = new List<GenericChart.GenericChart>();

            foreach (DataRow row in resultTable.Rows)
            {
                var rowData = new List<double>();
                string name = row[0].ToString();
                for (var i = 1; i < resultTable.Columns.Count; i++)
                {
                    // convert back to millions
                    rowData.Add(double.Parse(row[i].ToString()) * 1_000_000);
                }

               // var chart = Chart.Line<int, double, string>(columns, rowData, Name: name, MarkerColor: MapColor(name));
                var chart = Chart2D.Chart.Line<int, double, string>(columns, rowData, Name: name, MarkerColor: MapColor(name));
                charts.Add(chart);

                var combined = Chart.Combine(charts);

                combined
                    .WithLayout(MapTitle(mode, cacheSize))
                    .WithoutVerticalGridlines()
                    .WithAxisTitles("Number of threads", "Ops/sec")
                    .SaveSVG($"Results_{mode}_{cacheSize}", Width: 1000, Height: 600);
            }
        }

        public string MapTitle(Mode mode, int cacheSize)
        {
            string arch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString();
            string fwk = RuntimeInformation.FrameworkDescription.ToString();

            string os = "Win";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                os = "Mac";
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                os = "Linux";
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
            {
                os = "BSD";
            }

            switch (mode)
            {
                case Mode.Read:
                    return $"Read throughput, 100% cache hit ({os}/{arch}/{fwk})";
                case Mode.ReadWrite:
                    return $"Read + Write throughput ({os}/{arch}/{fwk})";
                case Mode.Update:
                    return $"Update throughput ({os}/{arch}/{fwk})";
                case Mode.Evict:
                    return $"Eviction throughput, 100% cache miss ({os}/{arch}/{fwk})";
                default:
                    return $"{mode} {cacheSize}";
            }
        }

        public Color MapColor(string name)
        {
            switch (name)
            {
                case "ClassicLru":
                    return Plotly.NET.Color.fromKeyword(Plotly.NET.ColorKeyword.Limegreen);
                case "MemoryCache":
                    return Plotly.NET.Color.fromKeyword(Plotly.NET.ColorKeyword.FireBrick);
                case "FastConcurrentLru":
                    return Plotly.NET.Color.fromKeyword(Plotly.NET.ColorKeyword.Silver);
                case "ConcurrentLru":
                    return Plotly.NET.Color.fromKeyword(Plotly.NET.ColorKeyword.RoyalBlue);
                case "ConcurrentLfu":
                    return Plotly.NET.Color.fromRGB(255, 192, 0);
                default:
                    return Plotly.NET.Color.fromKeyword(Plotly.NET.ColorKeyword.FireBrick);
            }
        }
    }

    public static class PlotExt
    {
        public static GenericChart.GenericChart WithAxisTitles(this GenericChart.GenericChart chart, string xTitle, string yTitle)
        {
            var font = new FSharpOption<Font>(Font.init(Size: new FSharpOption<double>(16)));
            FSharpOption<string> xt = new FSharpOption<string>(xTitle);
            FSharpOption<string> yt = new FSharpOption<string>(yTitle);
            return chart.WithXAxisStyle(Title.init(xt, Font: font)).WithYAxisStyle(Title.init(yt, Font: font));
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
