using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Diagnostics.Runtime.Interop;

namespace BitFaster.Caching.Benchmarks
{
    // Is it possible to write a class to eliminate the dispose code for types that are not IDisposable?
    // https://github.com/dotnet/runtime/issues/4920
    [SimpleJob(RuntimeMoniker.Net48)]
    [SimpleJob(RuntimeMoniker.Net60)]
    [DisassemblyDiagnoser(printSource: true, maxDepth:3)]
    [MemoryDiagnoser]
    public class DisposerBench
    {
        [Benchmark(Baseline = true)]
        public void HandWritten()
        {
            for (int i = 0; i < 1000; i++)
            {
                NotDisposable notDisposable = new NotDisposable();
                Disposable disposable = new Disposable();
                disposable.Dispose();
            }
        }

        [Benchmark()]
        public void NotOptimized()
        {
            for (int i = 0; i < 1000; i++)
            {
                NotDisposable notDisposable = new NotDisposable();
                Disposable disposable = new Disposable();

                if (notDisposable is IDisposable)
                {
                    ((IDisposable)notDisposable).Dispose();
                }

                if (disposable is IDisposable)
                {
                    ((IDisposable)disposable).Dispose();
                }
            }
        }

        [Benchmark()]
        public void GenericDisposerReadonlyProperty()
        {
            for (int i = 0; i < 1000; i++)
            {
                NotDisposable notDisposable = new NotDisposable();
                Disposable disposable = new Disposable();
                Disposer<Disposable>.Dispose(disposable);
                Disposer<NotDisposable>.Dispose(notDisposable);
            }
        }

        [Benchmark()]
        public void GenericDisposerStdCheck()
        {
            for (int i = 0; i < 1000; i++)
            {
                NotDisposable notDisposable = new NotDisposable();
                Disposable disposable = new Disposable();
                Disposer2<Disposable>.Dispose(disposable);
                Disposer2<NotDisposable>.Dispose(notDisposable);
            }
        }
    }

    public static class Disposer<T>
    {
        // try using a static readonly field
        private static readonly bool shouldDispose = typeof(IDisposable).IsAssignableFrom(typeof(T));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Dispose(T value)
        {
            if (shouldDispose)
            {
                ((IDisposable)value).Dispose();
            }
        }
    }

    public static class Disposer2<T>
    { 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Dispose(T value)
        {
            if (value is IDisposable d)
            {
                d.Dispose();
            }
        }
    }

    public class NotDisposable
    { }

    public class Disposable : IDisposable
    {
        private bool isDisposed = false;

        public void Dispose()
        {
            if (!isDisposed)
                this.isDisposed = true;
        }
    }
}
