using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
        private static Disposer<NotDisposable> notDisposableDisposer = new Disposer<NotDisposable>();
        private static Disposer<Disposable> disposableDisposer = new Disposer<Disposable>();

        private static Disposer2<NotDisposable> notDisposableDisposer2 = new Disposer2<NotDisposable>();
        private static Disposer2<Disposable> disposableDisposer2 = new Disposer2<Disposable>();

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

        //[Benchmark()]
        //public void GenericDisposer()
        //{
        //    for(int i = 0; i < 1000; i++)
        //    {
        //        NotDisposable notDisposable = new NotDisposable();
        //        Disposable disposable = new Disposable();
        //        disposableDisposer.Dispose(disposable);
        //        notDisposableDisposer.Dispose(notDisposable);
        //    }
        //}

        //[Benchmark()]
        //public void Oracle()
        //{
        //    for (int i = 0; i < 1000; i++)
        //    {
        //        NotDisposable notDisposable = new NotDisposable();
        //        Disposable disposable = new Disposable();
        //        Dispose(disposable);
        //        Dispose(notDisposable);
        //    }
        //}

        [Benchmark()]
        public void GenericDisposer2()
        {
            for (int i = 0; i < 1000; i++)
            {
                NotDisposable notDisposable = new NotDisposable();
                Disposable disposable = new Disposable();
                Disposer2<Disposable>.Dispose(disposable);
                Disposer2<NotDisposable>.Dispose(notDisposable);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Dispose<T>(T value)
        {
            if (default(DisposeOracle<T>).ShouldDispose())
            {
                ((IDisposable)value).Dispose();
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

    public struct DisposeOracle<T>
    {
        public bool ShouldDispose()
        {
            return typeof(T) is IDisposable;
        }
    }

    public struct Disposer2<T>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Dispose(T value)
        {
            switch (typeof(T))
            {
                case IDisposable:
                    ((IDisposable)value).Dispose(); 
                    break;
                default:
                    break;
            }
        }
    }

    public class Disposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
