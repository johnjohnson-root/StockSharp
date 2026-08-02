namespace StockSharp.Foundation.Collections;

/// <summary>
/// A collection that guards its state with a <see cref="Lock"/>.
/// </summary>
/// <remarks>
/// Exposes the lock, so callers and the <c>SyncGet</c>/<c>SyncDo</c> helpers
/// can hold it across several operations.
/// The lock is reentrant,
/// so an operation invoked while the same thread already holds it does not deadlock.
/// </remarks>
public interface ISynchronized
{
	/// <summary>
	/// Object the collection's members lock on.
	/// </summary>
	Lock SyncRoot { get; }
}
