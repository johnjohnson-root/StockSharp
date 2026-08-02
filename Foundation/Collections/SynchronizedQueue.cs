namespace StockSharp.Foundation.Collections;

/// <summary>
/// Thread-safe FIFO queue.
/// </summary>
/// <remarks>
/// Every member takes <see cref="SyncRoot"/>;
/// enumeration hands back a snapshot,
/// so a caller can enumerate while another thread mutates.
/// </remarks>
/// <typeparam name="T">Item type.</typeparam>
public class SynchronizedQueue<T> : IEnumerable<T>, ISynchronized
{
	private readonly Queue<T> _inner = [];

	/// <summary>
	/// Object all members lock on.
	/// </summary>
	public Lock SyncRoot { get; } = new();

	/// <summary>
	/// Takes <see cref="SyncRoot"/> for the duration of the returned scope.
	/// </summary>
	/// <returns>Lock scope.</returns>
	public Lock.Scope EnterScope() => SyncRoot.EnterScope();

	/// <summary>
	/// Item count.
	/// </summary>
	public int Count
	{
		get
		{
			using (SyncRoot.EnterScope())
				return _inner.Count;
		}
	}

	/// <summary>
	/// Adds an item to the tail.
	/// </summary>
	/// <param name="item">Item.</param>
	public void Enqueue(T item)
	{
		using (SyncRoot.EnterScope())
			_inner.Enqueue(item);
	}

	/// <summary>
	/// Removes and returns the head item.
	/// </summary>
	/// <returns>Head item.</returns>
	/// <exception cref="InvalidOperationException">The queue is empty.</exception>
	public T Dequeue()
	{
		using (SyncRoot.EnterScope())
			return _inner.Dequeue();
	}

	/// <summary>
	/// Removes and returns the head item when the queue is not empty.
	/// </summary>
	/// <param name="item">Head item.</param>
	/// <returns><see langword="true"/> if an item was dequeued.</returns>
	public bool TryDequeue(out T item)
	{
		using (SyncRoot.EnterScope())
			return _inner.TryDequeue(out item);
	}

	/// <summary>
	/// Returns the head item without removing it.
	/// </summary>
	/// <returns>Head item.</returns>
	/// <exception cref="InvalidOperationException">The queue is empty.</exception>
	public T Peek()
	{
		using (SyncRoot.EnterScope())
			return _inner.Peek();
	}

	/// <summary>
	/// Removes all items.
	/// </summary>
	public void Clear()
	{
		using (SyncRoot.EnterScope())
			_inner.Clear();
	}

	/// <inheritdoc />
	public IEnumerator<T> GetEnumerator()
	{
		T[] snapshot;

		using (SyncRoot.EnterScope())
			snapshot = [.. _inner];

		return ((IEnumerable<T>)snapshot).GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
