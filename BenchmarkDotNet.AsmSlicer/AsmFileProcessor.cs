namespace BenchmarkDotNet.AsmSlicer;

public class AsmFileProcessor
{
    private string filePath;
    public AsmFileProcessor(string file)
    { 
        this.filePath = file;
    }

    public void Process()
    {
        string path = Path.GetDirectoryName(filePath) ?? throw new InvalidOperationException("File path does not contain a directory name");
        string fileName = Path.GetFileName(filePath);
        string benchmark = fileName.Substring(0, fileName.Length-7);
        string outputPath = Path.Combine(path, benchmark);

        if (Directory.Exists(outputPath))
        {
            Directory.Delete(outputPath, true);
        }

        Directory.CreateDirectory(outputPath);

        BenchMethodAsmWriter? bw = null;

        using (var sr = new StreamReader(filePath))
        {
            string? line = sr.ReadLine();

            while (line != null)
            {
                if (line.StartsWith("##"))
                {
                    var framework = line.TrimStart('#', ' ');
                    sr.ReadLine(); // ```assembly
                    line = sr.ReadLine(); // benchmarkname

                    string? methodName = line?.TrimStart(';', ' ').TrimEnd('(', ')');

                    if (bw != null)
                    {
                        bw.Dispose();
                    }

                    string frameworkPath = Path.Combine(outputPath, framework);
                    if (!Directory.Exists(frameworkPath))
                    {
                        Directory.CreateDirectory(frameworkPath);
                    }

                    bw = new BenchMethodAsmWriter(frameworkPath, methodName);
                }
                else
                {
                    bw?.WriteLine(line);
                }

                line = sr.ReadLine();
            }
        }
    }
}

