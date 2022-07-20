using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    // marker interface enables type constraints
    public interface IScoped<T> where T : IDisposable
    { }
}
