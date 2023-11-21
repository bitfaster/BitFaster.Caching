using BenchPlot;
using CsvHelper;
using Plotly.NET;
using Plotly.NET.ImageExport;
using System.Data;
using System.Globalization;
using Chart = Plotly.NET.CSharp.Chart;

if (args.Length != 1)
{
    Console.WriteLine($"Invalid args");
    return 1;
}

string inputPath = args[0];
string outputPath = Path.Combine(inputPath, "plots");

Console.WriteLine($"Looking for results dir at {inputPath}");

string[] files = System.IO.Directory.GetFiles(Path.Combine(inputPath, "results"), "*.csv");

if (files.Length == 0)
{
    Console.WriteLine($"No benchmark results found");
    return 2;
}
else
{
    Console.WriteLine($"Found {files.Length} benchmark results.");
}

if (!Directory.Exists(outputPath))
{
    Console.WriteLine($"Creating plots dir at {outputPath}");
    Directory.CreateDirectory(outputPath);
}

foreach (string file in files)
{
    string benchName = Path.GetFileNameWithoutExtension(file).Replace("-report", string.Empty);

    Console.WriteLine($"Processing {benchName}");

    using (var reader = new StreamReader(file))
    using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
    {
        csv.Context.RegisterClassMap<BenchMap>();
        var records = csv.GetRecords<BenchmarkData>();

        var jobs = records.GroupBy(r => r.Job).ToList();

        // plot one column chart for each job
        foreach (var job in jobs)
        {
            Console.WriteLine($"Plotting {job.Key}...");

            var methods = job.Select(r => r.Method).ToArray();
            var nanos = job.Select(r => TimeParser.Parse(r.Mean)).ToArray();
            var stdDev = job.Select(r => TimeParser.Parse(r.StdDev)).ToArray();

            var fn = $"{benchName} ({job.Key})";
            var chart = Chart.Column<double, string, string>(nanos, methods, MarkerColor: Plotly.NET.Color.fromKeyword(Plotly.NET.ColorKeyword.IndianRed))
                .WithYErrorStyle<double, IConvertible>(stdDev)
                .WithAxisTitles("Time (ns)")
                .WithLayout(fn);

            chart.SaveSVG(Path.Combine(outputPath, fn), Width: 1000, Height: 600);
        }

        // plot a column chart with results grouped by job
        if (jobs.Count() > 1)
        {
            Console.WriteLine($"Plotting combined jobs...");

            List<ColorKeyword> colors = new List<ColorKeyword>() { ColorKeyword.IndianRed, ColorKeyword.Salmon };
            List<GenericChart.GenericChart> charts = new List<GenericChart.GenericChart>();

            int ind = 0;
            foreach (var job in jobs)
            {
                var methods = job.Select(r => r.Method).ToArray();
                var nanos = job.Select(r => TimeParser.Parse(r.Mean)).ToArray();
                var stdDev = job.Select(r => TimeParser.Parse(r.StdDev)).ToArray();

                var chart = Chart.Column<double, string, string>(nanos, methods, job.Key, MarkerColor: Plotly.NET.Color.fromKeyword(colors[ind++]))
                    .WithYErrorStyle<double, IConvertible>(stdDev);

                charts.Add(chart);
            }

            var combined = Chart.Combine(charts);

            var fn = $"{benchName}";
            combined
                .WithAxisTitles("Time (ns)")
                .WithLayout(fn)
                .SaveSVG(Path.Combine(outputPath, fn), Width: 1000, Height: 600);
        }
    }
}

return 0;
