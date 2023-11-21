using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using Microsoft.FSharp.Core;
using Plotly.NET;
using Plotly.NET.ImageExport;
using Plotly.NET.LayoutObjects;
using static Plotly.NET.StyleParam.SubPlotId;
using Chart = Plotly.NET.CSharp.Chart;

namespace BitFaster.Caching.ThroughputAnalysis
{
    public class Exporter
    {
        DataTable resultTable = new DataTable();

        public Exporter(int maxThreads)
        {
            // output:
            // ThreadCount   1  2  3  4  5
            // Classic       5  6  7  7  8
            // Concurrent    5  6  7  7  8

            resultTable.Clear();
            resultTable.Columns.Add("ThreadCount");
            foreach (var tc in Enumerable.Range(1, maxThreads).ToArray())
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
            var columns = new List<string>();

            foreach (DataColumn column in resultTable.Columns)
            {
                columns.Add(column.ColumnName);
            }

            List<GenericChart.GenericChart> charts = new List<GenericChart.GenericChart>();

            foreach (DataRow row in resultTable.Rows)
            {
                var rowData = new List<double>();
                string name = row[0].ToString();
                for (var i = 1; i < resultTable.Columns.Count; i++)
                {
                    rowData.Add(double.Parse(row[i].ToString()));
                }

                var chart = Chart.Line<string, double, string>(columns, rowData, Name: name, MarkerColor: Plotly.NET.Color.fromKeyword(Plotly.NET.ColorKeyword.FireBrick));
                charts.Add(chart);

                var combined = Chart.Combine(charts);

                combined
                    .WithLayout(MapTitle(mode, cacheSize))
                    .WithoutVerticalGridlines()
                    .WithAxisTitles("Number of threads", "Ops/sec (millions)")
                    .SaveSVG($"Results_{mode}_{cacheSize}", Width: 1000, Height: 600);
            }
        }

        public string MapTitle(Mode mode, int cacheSize)
        {
            switch (mode)
            {
                case Mode.Read:
                    return $"Read throughput (100% cache hit) for size {cacheSize}";
                case Mode.ReadWrite:
                    return $"Read + Write throughput for size {cacheSize}";
                case Mode.Update:
                    return $"Update throughput for size {cacheSize}";
                case Mode.Evict:
                    return $"Eviction throughput (100% cache miss) for size {cacheSize}";
                default:
                    return $"{mode} {cacheSize}";
            }
        }
    }

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
            FSharpOption<Color> plotBGColor = new FSharpOption<Color>(Color.fromKeyword(Plotly.NET.ColorKeyword.WhiteSmoke));
            Layout layout = Layout.init<IConvertible>(PlotBGColor: plotBGColor, Title: t);
            return chart.WithLayout(layout);
        }
    }
}
