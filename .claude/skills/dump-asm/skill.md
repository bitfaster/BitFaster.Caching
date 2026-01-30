---
name: dump-asm
description: Generate assembly for code exercised by a benchmark and organize output into a baseline directory named after the current git branch. Use to generate assembly code that can be diff'd between branches.
---

## Usage

```
/dump-asm <BenchmarkName> [<Runtimes>]
```

## Arguments

- `$ARGUMENTS` - The name of the benchmark class to run (e.g., `LruJustGetOrAdd`, `LfuJustGetOrAdd`, `SketchIncrement`), optionally followed by a list of one or more runtimes (e.g., `net48`, `net9.0` or `net48 net9.0`)

## Instructions

This skill orchestrates benchmark assembly generation and organizes the output for comparison.

Parse the arguments: the first argument is the benchmark name, and the optional second argument is the list of runtimes.

### Step 1: Clean artifacts

Delete the BenchmarkDotNet.Artifacts directory to ensure a clean run:

```bash
rm -rf BenchmarkDotNet.Artifacts
```

### Step 2: Run benchmark

Invoke the `/bench-fast` skill with the provided benchmark name and optional runtime to generate assembly code:

```
/bench-fast <BenchmarkName> [<Runtimes>]
```

### Step 3: Split assembly files

Invoke the `/split-asm` skill to generate individual assembly code files:

```
/split-asm
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
