namespace StockSharp.Foundation.Collections;

/// <summary>
/// Thread-safe FIFO queue.
/// </summary>
/// <typeparam name="T">Item type.</typeparam>
public class SynchronizedQueue<T> : IEnumerable<T>
{
	private readonly Queue<T> _inner = [];

	/// <summary>
	/// Object all members lock on.
	/// </summary>
	public Lock SyncRoot { get; } = new();

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
	/// Add an item to the tail.
	/// </summary>
	/// <param name="item">Item.</param>
	public void Enqueue(T item)
	{
		using (SyncRoot.EnterScope())
			_inner.Enqueue(item);
	}

	/// <summary>
	/// Remove and return the head item.
	/// </summary>
	/// <returns>Head item.</returns>
	public T Dequeue()
	{
		using (SyncRoot.EnterScope())
			return _inner.Dequeue();
	}

	/// <summary>
	/// Remove and return the head item if the queue is not empty.
	/// </summary>
	/// <param name="item">Head item.</param>
	/// <returns><see langword="true"/> if an item was dequeued.</returns>
	public bool TryDequeue(out T item)
	{
		using (SyncRoot.EnterScope())
			return _inner.TryDequeue(out item);
	}

	/// <summary>
	/// Return the head item without removing it.
	/// </summary>
	/// <returns>Head item.</returns>
	public T Peek()
	{
		using (SyncRoot.EnterScope())
			return _inner.Peek();
	}

	/// <summary>
	/// Remove all items.
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
