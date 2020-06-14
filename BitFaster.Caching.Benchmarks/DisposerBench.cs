using System;
using System.Collections.Generic;
using System.Text;
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Diagnostics.Runtime.Interop;

namespace BitFaster.Caching.Benchmarks
{
    // Is it possible to write a class to eliminate the dispose code for types that are not IDisposable?
    // https://github.com/dotnet/runtime/issues/4920
    public class DisposerBench
    {
        private Disposer<NotDisposable> notDisposableDisposer = new Disposer<NotDisposable>();
        private Disposer<Disposable> disposableDisposer = new Disposer<Disposable>();

        [Benchmark(Baseline = true)]
        public void NotDisposable()
        {
            for (int i = 0; i < 1000; i++)
            {
                NotDisposable notDisposable = new NotDisposable();
                notDisposableDisposer.Dispose(notDisposable); 
            }
        }

        [Benchmark()]
        public void Disposable()
        {
            for (int i = 0; i < 1000; i++)
            {
                Disposable disposable = new Disposable();
                disposableDisposer.Dispose(disposable); 
            }
        }

    }

    public struct Disposer<T>
    {
        public void Dispose(T value)
        {
            if (typeof(T) is IDisposable)
            {
                ((IDisposable)value).Dispose();
            }
        }
    }

    public class NotDisposable
    { }

    public class Disposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
