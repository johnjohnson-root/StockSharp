namespace StockSharp.Foundation.Collections;

/// <summary>
/// Thread-safe set.
/// </summary>
/// <remarks>
/// <see cref="Add"/> returns <see langword="void"/> and silently ignores a duplicate,
/// matching <see cref="ICollection{T}"/>; use <see cref="TryAdd"/> when the caller needs
/// to know whether the item was new. Enumeration hands back a snapshot.
/// </remarks>
/// <typeparam name="T">Item type.</typeparam>
public class SynchronizedSet<T> : ICollection<T>, ISynchronized
{
	private readonly HashSet<T> _inner;

	/// <summary>
	/// Initializes a new instance of the <see cref="SynchronizedSet{T}"/>.
	/// </summary>
	public SynchronizedSet()
		=> _inner = [];

	/// <summary>
	/// Initializes a new instance with the specified comparer.
	/// </summary>
	/// <param name="comparer">Item comparer.</param>
	public SynchronizedSet(IEqualityComparer<T> comparer)
		=> _inner = new(comparer);

	/// <summary>
	/// Object all members lock on.
	/// </summary>
	public Lock SyncRoot { get; } = new();

	/// <summary>
	/// The underlying set. Only touch it while holding <see cref="SyncRoot"/>.
	/// </summary>
	protected HashSet<T> Inner => _inner;

	/// <summary>
	/// Take <see cref="SyncRoot"/> for the duration of the returned scope.
	/// </summary>
	/// <returns>Lock scope.</returns>
	public Lock.Scope EnterScope() => SyncRoot.EnterScope();

	/// <summary>
	/// Called after any mutation, while <see cref="SyncRoot"/> is held.
	/// </summary>
	protected virtual void OnChanged()
	{
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
	public void Add(T item) => TryAdd(item);

	/// <summary>
	/// Add the item, reporting whether it was not already present.
	/// </summary>
	/// <param name="item">Item.</param>
	/// <returns><see langword="true"/> if the item was added.</returns>
	public bool TryAdd(T item)
	{
		using (SyncRoot.EnterScope())
		{
			if (!_inner.Add(item))
				return false;

			OnChanged();
			return true;
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
/// <see cref="SynchronizedSet{T}"/> that also exposes a cached snapshot array.
/// </summary>
/// <typeparam name="T">Item type.</typeparam>
public class CachedSynchronizedSet<T> : SynchronizedSet<T>
{
	private T[] _cache;

	/// <summary>
	/// Initializes a new instance of the <see cref="CachedSynchronizedSet{T}"/>.
	/// </summary>
	public CachedSynchronizedSet()
	{
	}

	/// <summary>
	/// Initializes a new instance with the specified comparer.
	/// </summary>
	/// <param name="comparer">Item comparer.</param>
	public CachedSynchronizedSet(IEqualityComparer<T> comparer)
		: base(comparer)
	{
	}

	/// <inheritdoc />
	protected override void OnChanged()
	{
		_cache = null;
		base.OnChanged();
	}

	/// <summary>
	/// Snapshot of the items.
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
