namespace StockSharp.Foundation.Collections;

/// <summary>
/// Thread-safe <see cref="IList{T}"/>.
/// </summary>
/// <remarks>
/// Every member takes <see cref="SyncRoot"/>; enumeration hands back a snapshot so a
/// caller can enumerate while another thread mutates.
/// </remarks>
/// <typeparam name="T">Item type.</typeparam>
public class SynchronizedList<T> : IList<T>
{
	private readonly List<T> _inner;

	/// <summary>
	/// Initializes a new instance of the <see cref="SynchronizedList{T}"/>.
	/// </summary>
	public SynchronizedList()
		=> _inner = [];

	/// <summary>
	/// Initializes a new instance with the specified initial capacity.
	/// </summary>
	/// <param name="capacity">Initial capacity.</param>
	public SynchronizedList(int capacity)
		=> _inner = new(capacity);

	/// <summary>
	/// Object all members lock on.
	/// </summary>
	public Lock SyncRoot { get; } = new();

	/// <summary>
	/// The underlying list. Only touch it while holding <see cref="SyncRoot"/>.
	/// </summary>
	protected List<T> Inner => _inner;

	/// <summary>
	/// Called after any mutation, while <see cref="SyncRoot"/> is held.
	/// </summary>
	protected virtual void OnChanged()
	{
	}

	/// <inheritdoc />
	public T this[int index]
	{
		get
		{
			using (SyncRoot.EnterScope())
				return _inner[index];
		}
		set
		{
			using (SyncRoot.EnterScope())
			{
				_inner[index] = value;
				OnChanged();
			}
		}
	}

	/// <inheritdoc />
	public int Count
	{
		get
		{
			using (SyncRoot.EnterScope())
				return _inner.Count;
		}
	}

	/// <inheritdoc />
	public bool IsReadOnly => false;

	/// <inheritdoc />
	public void Add(T item)
	{
		using (SyncRoot.EnterScope())
		{
			_inner.Add(item);
			OnChanged();
		}
	}

	/// <inheritdoc />
	public void Insert(int index, T item)
	{
		using (SyncRoot.EnterScope())
		{
			_inner.Insert(index, item);
			OnChanged();
		}
	}

	/// <inheritdoc />
	public bool Remove(T item)
	{
		using (SyncRoot.EnterScope())
		{
			if (!_inner.Remove(item))
				return false;

			OnChanged();
			return true;
		}
	}

	/// <inheritdoc />
	public void RemoveAt(int index)
	{
		using (SyncRoot.EnterScope())
		{
			_inner.RemoveAt(index);
			OnChanged();
		}
	}

	/// <inheritdoc />
	public void Clear()
	{
		using (SyncRoot.EnterScope())
		{
			if (_inner.Count == 0)
				return;

			_inner.Clear();
			OnChanged();
		}
	}

	/// <inheritdoc />
	public bool Contains(T item)
	{
		using (SyncRoot.EnterScope())
			return _inner.Contains(item);
	}

	/// <inheritdoc />
	public int IndexOf(T item)
	{
		using (SyncRoot.EnterScope())
			return _inner.IndexOf(item);
	}

	/// <inheritdoc />
	public void CopyTo(T[] array, int arrayIndex)
	{
		using (SyncRoot.EnterScope())
			_inner.CopyTo(array, arrayIndex);
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

/// <summary>
/// <see cref="SynchronizedList{T}"/> that also exposes a cached snapshot array.
/// </summary>
/// <typeparam name="T">Item type.</typeparam>
public class CachedSynchronizedList<T> : SynchronizedList<T>
{
	private T[] _cache;

	/// <summary>
	/// Initializes a new instance of the <see cref="CachedSynchronizedList{T}"/>.
	/// </summary>
	public CachedSynchronizedList()
	{
	}

	/// <summary>
	/// Initializes a new instance with the specified initial capacity.
	/// </summary>
	/// <param name="capacity">Initial capacity.</param>
	public CachedSynchronizedList(int capacity)
		: base(capacity)
	{
	}

	/// <inheritdoc />
	protected override void OnChanged()
	{
		_cache = null;
		base.OnChanged();
	}

	/// <summary>
	/// Snapshot of the items, in list order.
	/// </summary>
	public T[] Cache
	{
		get
		{
			using (SyncRoot.EnterScope())
				return _cache ??= [.. Inner];
		}
	}
}
