# dump-asm

Generate assembly code for a benchmark and organize output into a baseline directory named after the current git branch.

## Usage

```
/dump-asm <BenchmarkName> [<Framework>]
```

## Arguments

- `$ARGUMENTS` - The name of the benchmark class to run (e.g., `LruJustGetOrAdd`, `LfuJustGetOrAdd`, `SketchIncrement`), optionally followed by a target framework (e.g., `net9.0`, `net8.0`, `net6.0`)

## Instructions

This skill orchestrates benchmark assembly generation and organizes the output for comparison.

Parse the arguments: the first argument is the benchmark name, and the optional second argument is the target framework.

### Step 1: Clean artifacts

Delete the BenchmarkDotNet.Artifacts directory to ensure a clean run:

```bash
rm -rf BenchmarkDotNet.Artifacts
```

### Step 2: Run benchmark

Run the bench-fast skill with the provided benchmark name and optional framework to generate assembly code.

If a framework is specified, execute:

```bash
dotnet run -c Release --project BitFaster.Caching.Benchmarks --framework <Framework> --filter "<BenchmarkName>" -j short --warmupCount 3 --iterationCount 5 -d --disasmDepth 5
```

If no framework is specified, default to `net9.0`:

```bash
dotnet run -c Release --project BitFaster.Caching.Benchmarks --framework net9.0 --filter "<BenchmarkName>" -j short --warmupCount 3 --iterationCount 5 -d --disasmDepth 5
```

### Step 3: Split assembly files

Run the split-asm skill to generate individual assembly code files:

```bash
dotnet run --project C:/repo/splitasm/splitasm -- BenchmarkDotNet.Artifacts/results
```

### Step 4: Organize into baseline directory

Get the current git branch name and convert it to a valid directory name by replacing forward slashes with dashes:

```bash
git rev-parse --abbrev-ref HEAD | tr '/' '-'
```

For example, `users/alexpeck/foo` becomes `users-alexpeck-foo`.

Create the baseline directory structure preserving the benchmark name and runtime hierarchy. For each benchmark and runtime combination found in `BenchmarkDotNet.Artifacts/results/`:

1. Extract the short benchmark name from the full benchmark path (e.g., `BitFaster.Caching.Benchmarks.LruJustGetOrAdd` → `LruJustGetOrAdd`)
2. Create the directory `baseline/<sanitized-branch-name>/<benchmarkname>/<runtime>/`
3. Copy all files from the corresponding `BenchmarkDotNet.Artifacts/results/<full-benchmark-name>/<runtime>/` directory

The final structure should be:
```
baseline/
  <sanitized-branch-name>/
    <benchmarkname>/
      <runtime>/
        <MethodName>-asm.md
        <MethodName>-summary.md
        ...
```

For example:
```
baseline/
  users-alexpeck-skills/
    LruJustGetOrAdd/
      .NET 6.0.36 (6.0.3624.51421), X64 RyuJIT AVX2/
        FastConcurrentLru-asm.md
        FastConcurrentLru-summary.md
        ...
```
