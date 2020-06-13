using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Lightweight.Caching
{
	public class ScopedDisposable<T> : IDisposable where T : IDisposable
    {
		private ReferenceCount<T> refCount;
		private bool isDisposed;

        public ScopedDisposable(T value)
        {
            this.refCount = new ReferenceCount<T>(value);
        }

        public Lifetime CreateLifetime()
        {
			if (this.isDisposed)
			{
				throw new ObjectDisposedException($"{nameof(T)} is disposed.");
			}

			try
            {
				while (true)
				{
					// IncrementCopy will throw InvalidOperationException if the referenced object has no references.
					// This mitigates the race where the value is disposed after the above check is run.
					var oldRefCount = this.refCount;
					var newRefCount = oldRefCount.IncrementCopy();

					if (oldRefCount == Interlocked.CompareExchange(ref this.refCount, newRefCount, oldRefCount))
					{
						// When Lease is disposed, it calls DecrementReferenceCount
						return new Lifetime(oldRefCount.Value, this.DecrementReferenceCount);
					}
				}
			}
			catch (InvalidOperationException)
            {
				throw new ObjectDisposedException($"{nameof(T)} is disposed.");
			}
		}

		private void DecrementReferenceCount()
		{
			while (true)
			{
				var oldRefCount = this.refCount;
				var newRefCount = oldRefCount.DecrementCopy();

				if (oldRefCount == Interlocked.CompareExchange(ref this.refCount, newRefCount, oldRefCount))
				{
					if (newRefCount.Count == 0)
                    {
						newRefCount.Value.Dispose();
                    }

					break;
				}
			}
		}

		public void Dispose()
        {
			if (!this.isDisposed)
			{
				this.DecrementReferenceCount();
				this.isDisposed = true;
			}
		}

		public class Lifetime : IDisposable
        {
			private readonly Action onDisposeAction;
			private bool isDisposed;

			public Lifetime(T value, Action onDisposeAction)
            {
				this.Value = value;
				this.onDisposeAction = onDisposeAction;
            }

			public T Value { get; }

			public void Dispose()
			{
				if (!this.isDisposed)
				{
					this.onDisposeAction();
					this.isDisposed = true;
				}
			}
		}
    }
}
