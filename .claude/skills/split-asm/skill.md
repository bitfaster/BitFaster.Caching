---
name: split-asm
description: Split BenchmarkDotNet assembly markdown files into individual files per benchmark method using splitasm. Use to break one big assembly code file per benchmark into one file per benchmark method. 
---

## Usage

```
/split-asm [<ResultsPath>]
```

## Arguments

- `$ARGUMENTS` - Optional path to the BenchmarkDotNet results directory. Defaults to `BenchmarkDotNet.Artifacts/results` in the current repository.

## Instructions

Run splitasm to break down BenchmarkDotNet assembly markdown files into a single file per benchmark method. This enables using file diffs to compare how code changes affect disassembler output.

Parse the arguments: the optional first argument is the path to the results directory.

If a path is specified, execute:

```bash
dotnet run --project C:/repo/splitasm/splitasm -- <ResultsPath>
```

If no path is specified, default to the standard BenchmarkDotNet output location:

```bash
dotnet run --project C:/repo/splitasm/splitasm -- BenchmarkDotNet.Artifacts/results
```

The tool produces:
1. Individual assembly files - one markdown file per benchmarked method containing its assembly code
2. A summary file listing disassembled code size in bytes for each benchmarked method

Output is organized hierarchically by target benchmark, then by target framework.

## Prerequisites

The splitasm repository should be cloned to C:/repo/splitasm. If not available, clone from https://github.com/bitfaster/splitasm:

```bash
cd C:/repo && git clone https://github.com/bitfaster/splitasm.git
```
