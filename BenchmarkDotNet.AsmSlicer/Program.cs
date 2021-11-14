global using BenchmarkDotNet.AsmSlicer;

// Break down the .asm markdown files into 1 file per benchmark method, making it easy to diff
string resultPath = args[0];
Console.WriteLine($"Searching {resultPath} for .asm files");

try
{
    string[] files = System.IO.Directory.GetFiles(resultPath, "*asm.md");

    Console.WriteLine($"Found {files.Length} .asm files");

    foreach (var file in files)
    {
        Console.WriteLine($"Processing {file}");

        try
        {
            var s = new AsmFileProcessor(file);
            s.Process();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to process {file}");
            Console.WriteLine(ex.Message);
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Failed to process {resultPath}");
    Console.WriteLine(ex.Message);
}
