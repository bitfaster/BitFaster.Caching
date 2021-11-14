namespace BenchmarkDotNet.AsmSlicer;

public class BenchMethodAsmWriter : IDisposable
{
    private readonly StreamWriter writer;

    // 1 .asm file per method name
    public BenchMethodAsmWriter(string path, string? methodName)
    {
        string filePath = Path.Combine(path, $"{DeNamespace(methodName)}-asm.md");

        FileStreamOptions fileStreamOptions = new FileStreamOptions();
        fileStreamOptions.Access = FileAccess.Write;
        fileStreamOptions.Mode = FileMode.Create;

        this.writer = new StreamWriter(filePath, fileStreamOptions);
        this.writer.WriteLine("```assembly");
        this.writer.WriteLine($"; {methodName}()"); // reconstruct
    }

    public string? DeNamespace(string? methodName)
    {
        int s = methodName?.LastIndexOf(".") ?? 0;
        return methodName?.Substring(s + 1);
    }

    public void WriteLine(string line)
    {
        this.writer.WriteLine(line);
    }

    public void Dispose()
    {
        this.writer.Dispose();
    }
}

