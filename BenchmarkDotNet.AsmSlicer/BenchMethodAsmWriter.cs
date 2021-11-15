namespace BenchmarkDotNet.AsmSlicer;

public class BenchMethodAsmWriter : IDisposable
{
    private readonly StreamWriter asmWriter;
    private readonly StreamWriter summaryWriter;

    private string? currentMethod;

    private List<MethodSummary> methodSummaries;

    // 1 .asm file per method name
    public BenchMethodAsmWriter(string path, string? methodName)
    {
        this.currentMethod = methodName;

        string asmFilePath = Path.Combine(path, $"{DeNamespace(methodName)}-asm.md");
        string summaryPath = Path.Combine(path, $"{DeNamespace(methodName)}-summary.md");

        FileStreamOptions fileStreamOptions = new FileStreamOptions();
        fileStreamOptions.Access = FileAccess.Write;
        fileStreamOptions.Mode = FileMode.Create;

        this.asmWriter = new StreamWriter(asmFilePath, fileStreamOptions);
        this.summaryWriter = new StreamWriter(summaryPath, fileStreamOptions);


        this.asmWriter.WriteLine("```assembly");
        this.asmWriter.WriteLine($"; {methodName}()"); // reconstruct

        this.summaryWriter.WriteLine("| #  | Method      | Size (bytes) |");
        this.summaryWriter.WriteLine("| -- | ----------- | ------------ |");

        this.methodSummaries = new List<MethodSummary>();
    }

    private static string? DeNamespace(string? methodName)
    {
        int s = methodName?.LastIndexOf(".") ?? 0;
        return methodName?.Substring(s + 1);
    }

    public void WriteLine(string line)
    {
        if (line.StartsWith("; Total bytes of code"))
        {
            string size = line.Replace("; Total bytes of code ", string.Empty);
            this.methodSummaries.Add(new MethodSummary { Name = this.currentMethod, Size = size });
        }
        else if (line.StartsWith(";"))
        {
            this.currentMethod = line?.TrimStart(';', ' ').TrimEnd('(', ')');
        }

        this.asmWriter.WriteLine(line);
    }

    public void Dispose()
    {
        this.asmWriter.Dispose();
        WriteSummary();
        this.summaryWriter.Dispose();
    }

    private void WriteSummary()
    {
        int count = 0;
        foreach (var m in this.methodSummaries.OrderBy(ms => ms.Name))
        {
            this.summaryWriter.WriteLine($"| {count++} | {m.Name} | {m.Size} |");
        }
    }

    private record MethodSummary
    {
        public string? Name { get; init; }
        public string? Size { get; init; }
    }
}

