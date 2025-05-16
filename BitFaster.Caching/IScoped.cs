using System;

namespace BitFaster.Caching
{
    /// <summary>
    /// A marker interface for scopes to enable type constraints.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IScoped<T> where T : IDisposable
    { }
}
