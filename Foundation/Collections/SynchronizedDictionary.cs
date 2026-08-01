namespace StockSharp.Foundation.Collections;

/// <summary>
/// Thread-safe <see cref="IDictionary{TKey, TValue}"/>.
/// </summary>
/// <remarks>
/// Every member takes <see cref="SyncRoot"/>. Enumeration and the <see cref="Keys"/> /
/// <see cref="Values"/> collections hand back snapshots rather than live views, so a
/// caller can enumerate while another thread mutates without risking
/// <see cref="InvalidOperationException"/>. Callers needing several operations to be
/// atomic with respect to each other should hold <see cref="SyncRoot"/> across them.
/// </remarks>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TValue">Value type.</typeparam>
public class SynchronizedDictionary<TKey, TValue> : IDictionary<TKey, TValue>
{
	private readonly Dictionary<TKey, TValue> _inner;

	/// <summary>
	/// Initializes a new instance of the <see cref="SynchronizedDictionary{TKey, TValue}"/>.
	/// </summary>
	public SynchronizedDictionary()
		=> _inner = [];

	/// <summary>
	/// Initializes a new instance with the specified initial capacity.
	/// </summary>
	/// <param name="capacity">Initial capacity.</param>
	public SynchronizedDictionary(int capacity)
		=> _inner = new(capacity);

	/// <summary>
	/// Initializes a new instance with the specified key comparer.
	/// </summary>
	/// <param name="comparer">Key comparer.</param>
	public SynchronizedDictionary(IEqualityComparer<TKey> comparer)
		=> _inner = new(comparer);

	/// <summary>
	/// Object all members lock on. Hold it to make a sequence of operations atomic.
	/// </summary>
	public Lock SyncRoot { get; } = new();

	/// <summary>
	/// The underlying dictionary. Only touch it while holding <see cref="SyncRoot"/>.
	/// </summary>
	protected Dictionary<TKey, TValue> Inner => _inner;

	/// <summary>
	/// Called after any mutation, while <see cref="SyncRoot"/> is held.
	/// </summary>
	protected virtual void OnChanged()
	{
	}

	/// <inheritdoc />
	public TValue this[TKey key]
	{
		get
		{
			using (SyncRoot.EnterScope())
				return _inner[key];
		}
		set
		{
			using (SyncRoot.EnterScope())
			{
				_inner[key] = value;
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
	public ICollection<TKey> Keys
	{
		get
		{
			using (SyncRoot.EnterScope())
				return [.. _inner.Keys];
		}
	}

	/// <inheritdoc />
	public ICollection<TValue> Values
	{
		get
		{
			using (SyncRoot.EnterScope())
				return [.. _inner.Values];
		}
	}

	/// <inheritdoc />
	public void Add(TKey key, TValue value)
	{
		using (SyncRoot.EnterScope())
		{
			_inner.Add(key, value);
			OnChanged();
		}
	}

	/// <inheritdoc />
	public void Add(KeyValuePair<TKey, TValue> item)
		=> Add(item.Key, item.Value);

	/// <inheritdoc />
	public bool Remove(TKey key)
	{
		using (SyncRoot.EnterScope())
		{
			if (!_inner.Remove(key))
				return false;

			OnChanged();
			return true;
		}
	}

	/// <inheritdoc />
	public bool Remove(KeyValuePair<TKey, TValue> item)
	{
		using (SyncRoot.EnterScope())
		{
			if (!((ICollection<KeyValuePair<TKey, TValue>>)_inner).Remove(item))
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
	public bool ContainsKey(TKey key)
	{
		using (SyncRoot.EnterScope())
			return _inner.ContainsKey(key);
	}

	/// <inheritdoc />
	public bool Contains(KeyValuePair<TKey, TValue> item)
	{
		using (SyncRoot.EnterScope())
			return ((ICollection<KeyValuePair<TKey, TValue>>)_inner).Contains(item);
	}

	/// <inheritdoc />
	public bool TryGetValue(TKey key, out TValue value)
	{
		using (SyncRoot.EnterScope())
			return _inner.TryGetValue(key, out value);
	}

	/// <inheritdoc />
	public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
	{
		using (SyncRoot.EnterScope())
			((ICollection<KeyValuePair<TKey, TValue>>)_inner).CopyTo(array, arrayIndex);
	}

	/// <inheritdoc />
	public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
	{
		KeyValuePair<TKey, TValue>[] snapshot;

		using (SyncRoot.EnterScope())
			snapshot = [.. _inner];

		return ((IEnumerable<KeyValuePair<TKey, TValue>>)snapshot).GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// <see cref="SynchronizedDictionary{TKey, TValue}"/> that also exposes cached snapshot
/// arrays of its keys, values and pairs.
/// </summary>
/// <remarks>
/// The snapshots let callers enumerate without holding <see cref="SynchronizedDictionary{TKey, TValue}.SyncRoot"/>.
/// A snapshot already handed out is never mutated: a change to the dictionary discards
/// the cached array, and the next access builds a fresh one.
/// </remarks>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TValue">Value type.</typeparam>
public class CachedSynchronizedDictionary<TKey, TValue> : SynchronizedDictionary<TKey, TValue>
{
	private TKey[] _cachedKeys;
	private TValue[] _cachedValues;
	private KeyValuePair<TKey, TValue>[] _cachedPairs;

	/// <summary>
	/// Initializes a new instance of the <see cref="CachedSynchronizedDictionary{TKey, TValue}"/>.
	/// </summary>
	public CachedSynchronizedDictionary()
	{
	}

	/// <summary>
	/// Initializes a new instance with the specified initial capacity.
	/// </summary>
	/// <param name="capacity">Initial capacity.</param>
	public CachedSynchronizedDictionary(int capacity)
		: base(capacity)
	{
	}

	/// <summary>
	/// Initializes a new instance with the specified key comparer.
	/// </summary>
	/// <param name="comparer">Key comparer.</param>
	public CachedSynchronizedDictionary(IEqualityComparer<TKey> comparer)
		: base(comparer)
	{
	}

	/// <inheritdoc />
	protected override void OnChanged()
	{
		// discard rather than rebuild: a snapshot already handed out must stay unchanged,
		// and the next reader pays for the copy only if it actually wants one
		_cachedKeys = null;
		_cachedValues = null;
		_cachedPairs = null;

		base.OnChanged();
	}

	/// <summary>
	/// Snapshot of the keys.
	/// </summary>
	public TKey[] CachedKeys
	{
		get
		{
			using (SyncRoot.EnterScope())
				return _cachedKeys ??= [.. Inner.Keys];
		}
	}

	/// <summary>
	/// Snapshot of the values.
	/// </summary>
	public TValue[] CachedValues
	{
		get
		{
			using (SyncRoot.EnterScope())
				return _cachedValues ??= [.. Inner.Values];
		}
	}

	/// <summary>
	/// Snapshot of the key/value pairs.
	/// </summary>
	public KeyValuePair<TKey, TValue>[] CachedPairs
	{
		get
		{
			using (SyncRoot.EnterScope())
				return _cachedPairs ??= [.. Inner];
		}
	}
}
