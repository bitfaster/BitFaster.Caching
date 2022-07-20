using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    /// <summary>
    /// A marker interface for scopes to enable type constraints.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IScoped<T> where T : IDisposable
    { }
}
