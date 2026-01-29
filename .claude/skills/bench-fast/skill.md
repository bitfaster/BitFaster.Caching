# bench-fast

Run a benchmark quickly with minimal iterations to generate assembly code via DisassemblyDiagnoser.

## Usage

```
/bench-fast <BenchmarkName> [<Framework>]
```

## Arguments

- `$ARGUMENTS` - The name of the benchmark class to run (e.g., `LruJustGetOrAdd`, `LfuJustGetOrAdd`, `SketchIncrement`), optionally followed by a target framework (e.g., `net9.0`, `net8.0`, `net6.0`)

## Instructions

Run the specified benchmark from BitFaster.Caching.Benchmarks with minimal iterations using BenchmarkDotNet's dry job mode.

Parse the arguments: the first argument is the benchmark name, and the optional second argument is the target framework.

If a framework is specified, execute:

```bash
dotnet run -c Release --project BitFaster.Caching.Benchmarks --framework <Framework> -- --filter "<BenchmarkName>" -j short --warmupCount 3 --iterationCount 5 -d --disasmDepth 5
```

If no framework is specified, default to `net9.0`:

```bash
dotnet run -c Release --project BitFaster.Caching.Benchmarks --framework net9.0 -- --filter "<BenchmarkName>" -j short --warmupCount 3 --iterationCount 5 -d --disasmDepth 5
```

The `--warmupCount 3 --iterationCount 5` options reduce warmup and iteration counts for faster execution while still executing the code enough times to JIT optimized code.
