# bench-fast

Run a benchmark quickly with minimal iterations to generate assembly code via DisassemblyDiagnoser.

## Usage

```
/bench-fast <BenchmarkName> [<Runtimes>]
```

## Arguments

- `$ARGUMENTS` - The name of the benchmark class to run (e.g., `LruJustGetOrAdd`, `LfuJustGetOrAdd`, `SketchIncrement`), optionally followed by a list of one or more runtimes (e.g., `net48`, `net9.0` or `net48 net9.0`)

## Instructions

Run the specified benchmark from BitFaster.Caching.Benchmarks with minimal iterations using BenchmarkDotNet's command line.

Parse the arguments: the first argument is the benchmark name, and the optional second argument is the list of runtimes.

If a runtime arg is specified, execute:

```bash
dotnet run -c Release --project BitFaster.Caching.Benchmarks --framework net9.0 -- --runtimes <Runtimes> --filter "<BenchmarkName>" -j short --warmupCount 3 --iterationCount 5 -d --disasmDepth 5
```

If no runtime is specified, simply omit that command line arg:

```bash
dotnet run -c Release --project BitFaster.Caching.Benchmarks --framework net9.0 -- --filter "<BenchmarkName>" -j short --warmupCount 3 --iterationCount 5 -d --disasmDepth 5
```

The `--warmupCount 3 --iterationCount 5` options reduce warmup and iteration counts for faster execution while still executing the code enough times to JIT optimized code.
