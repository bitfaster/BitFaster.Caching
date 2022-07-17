using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.UnitTests
{
    public class DisposableValueFactory
    {
        public Disposable Disposable { get; } = new Disposable();

        public Scoped<Disposable> Create(int key)
        {
            return new Scoped<Disposable>(this.Disposable);
        }
    }
}
