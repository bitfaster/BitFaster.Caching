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

        //private static Disposer2<NotDisposable> notDisposableDisposer2 = new Disposer2<NotDisposable>();
        //private static Disposer2<Disposable> disposableDisposer2 = new Disposer2<Disposable>();

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

        //[Benchmark()]
        //public void GenericDisposer2()
        //{
        //    for (int i = 0; i < 1000; i++)
        //    {
        //        NotDisposable notDisposable = new NotDisposable();
        //        Disposable disposable = new Disposable();
        //        Disposer2<Disposable>.Dispose(disposable);
        //        Disposer2<NotDisposable>.Dispose(notDisposable);
        //    }
        //}

        [Benchmark()]
        public void GenericDisposer3()
        {
            for (int i = 0; i < 1000; i++)
            {
                NotDisposable notDisposable = new NotDisposable();
                Disposable disposable = new Disposable();
                Disposer3<Disposable>.Dispose(disposable);
                Disposer3<NotDisposable>.Dispose(notDisposable);
            }
        }

        // https://prodotnetmemory.com/slides/PerformancePatternsLong/#207
        [Benchmark()]
        public void IdomaticMarker()
        {
            for (int i = 0; i < 1000; i++)
            {
                NotDisposable notDisposable = new NotDisposable();
                Disposable disposable = new Disposable();
                DisposeMarker<DisposerPolicy<Disposable>, Disposable>(disposable);
                DisposeMarker<NoDisposerPolicy<NotDisposable>, NotDisposable>(notDisposable);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DisposeMarker<P, T>(T value) where P : struct, IDisposePolicy<T>
        {
            default(P).Dispose(value);
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

    public struct DisposeOracle<T>
    {
        public bool ShouldDispose()
        {
            return typeof(T) is IDisposable;
        }
    }

    // .net 5 only
    //public struct Disposer2<T>
    //{
    //    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //    public static void Dispose(T value)
    //    {
    //        switch (typeof(T))
    //        {
    //            case IDisposable:
    //                ((IDisposable)value).Dispose(); 
    //                break;
    //            default:
    //                break;
    //        }
    //    }
    //}

    public struct Disposer3<T>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Dispose(T value)
        {
            switch (value)
            {
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
                default:
                    break;
            }
        }
    }

    // idiomatic: marker interface + 2 policies

    public interface IDisposePolicy<T>
    {
        void Dispose(T value);
    }

    public struct DisposerPolicy<T> : IDisposePolicy<T> where T : IDisposable
    {
        public void Dispose(T value)
        {
            value.Dispose() ;
        }
    }

    public struct NoDisposerPolicy<T> : IDisposePolicy<T>
    {
        public void Dispose(T value)
        {
        }
    }
}
