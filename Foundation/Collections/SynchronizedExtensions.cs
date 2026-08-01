namespace StockSharp.Foundation.Collections;

/// <summary>
/// Atomic helper operations over collections, honouring a synchronized collection's lock.
/// </summary>
/// <remarks>
/// The helpers are declared on the collection interfaces (<see cref="IDictionary{TKey, TValue}"/>,
/// <see cref="ICollection{T}"/>) so they bind whether a call site holds the concrete Foundation
/// type or an interface reference — matching how the codebase declares its fields. When the
/// receiver is an <see cref="ISynchronized"/> collection, each helper holds its
/// <see cref="ISynchronized.SyncRoot"/> for the whole operation so a get-then-mutate cannot
/// interleave; on a plain collection it runs unlocked. The lock is reentrant, so the collection
/// members re-entered inside a helper (and helpers re-entered inside <see cref="SyncDo"/> /
/// <see cref="SyncGet"/>) do not deadlock.
///
/// Semantics are reconstructed from this repository's call sites, not from the previous
/// provider's source.
/// </remarks>
public static class SynchronizedExtensions
{
	private static void InLock(object collection, Action body)
	{
		if (collection is ISynchronized s)
		{
			using (s.SyncRoot.EnterScope())
				body();
		}
		else
			body();
	}

	private static TResult InLock<TResult>(object collection, Func<TResult> body)
	{
		if (collection is ISynchronized s)
		{
			using (s.SyncRoot.EnterScope())
				return body();
		}

		return body();
	}

	#region Dictionary

	/// <summary>
	/// Value for <paramref name="key"/>, creating and adding it with <paramref name="factory"/>
	/// when absent. The factory runs at most once and only when the key is missing.
	/// </summary>
	public static TValue SafeAdd<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, Func<TKey, TValue> factory)
		=> dict.SafeAdd(key, factory, out _);

	/// <summary>
	/// Value for <paramref name="key"/>, creating and adding it with <paramref name="factory"/>
	/// when absent, reporting via <paramref name="isNew"/> whether it was created.
	/// </summary>
	public static TValue SafeAdd<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, Func<TKey, TValue> factory, out bool isNew)
	{
		ArgumentNullException.ThrowIfNull(dict);
		ArgumentNullException.ThrowIfNull(factory);

		var created = false;

		var result = InLock(dict, () =>
		{
			if (dict.TryGetValue(key, out var value))
				return value;

			value = factory(key);
			dict.Add(key, value);
			created = true;
			return value;
		});

		isNew = created;
		return result;
	}

	/// <summary>
	/// Value for <paramref name="key"/>, creating a default-constructed value when absent.
	/// </summary>
	public static TValue SafeAdd<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key)
		where TValue : new()
		=> dict.SafeAdd(key, static _ => new TValue(), out _);

	/// <summary>
	/// Value for <paramref name="key"/>, creating a default-constructed value when absent,
	/// reporting via <paramref name="isNew"/> whether it was created.
	/// </summary>
	public static TValue SafeAdd<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, out bool isNew)
		where TValue : new()
		=> dict.SafeAdd(key, static _ => new TValue(), out isNew);

	/// <summary>
	/// Add the pair only when the key is absent, reporting whether it was added.
	/// </summary>
	public static bool TryAdd2<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue value)
	{
		ArgumentNullException.ThrowIfNull(dict);

		return InLock(dict, () =>
		{
			if (dict.ContainsKey(key))
				return false;

			dict.Add(key, value);
			return true;
		});
	}

	/// <summary>
	/// Remove the key, yielding the value it held.
	/// </summary>
	public static bool TryGetAndRemove<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, out TValue value)
	{
		ArgumentNullException.ThrowIfNull(dict);

		var found = false;
		var captured = default(TValue);

		InLock(dict, () =>
		{
			if (!dict.TryGetValue(key, out captured))
				return;

			dict.Remove(key);
			found = true;
		});

		value = captured;
		return found;
	}

	/// <summary>
	/// Remove the key and return the value it held, or the default when absent.
	/// </summary>
	public static TValue GetAndRemove<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key)
	{
		dict.TryGetAndRemove(key, out var value);
		return value;
	}

	#endregion

	#region Collection (list, set, and dictionary-as-collection-of-pairs)

	/// <summary>
	/// Add every item.
	/// </summary>
	public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> items)
	{
		ArgumentNullException.ThrowIfNull(collection);
		ArgumentNullException.ThrowIfNull(items);

		var buffer = items as IReadOnlyCollection<T> ?? [.. items];

		InLock(collection, () =>
		{
			foreach (var item in buffer)
				collection.Add(item);
		});
	}

	/// <summary>
	/// Remove every item in <paramref name="items"/> that is present; returns the count removed.
	/// </summary>
	public static int RemoveRange<T>(this ICollection<T> collection, IEnumerable<T> items)
	{
		ArgumentNullException.ThrowIfNull(collection);
		ArgumentNullException.ThrowIfNull(items);

		var buffer = items as IReadOnlyCollection<T> ?? [.. items];

		return InLock(collection, () =>
		{
			var count = 0;

			foreach (var item in buffer)
			{
				if (collection.Remove(item))
					count++;
			}

			return count;
		});
	}

	/// <summary>
	/// Remove every item matching <paramref name="predicate"/>; returns the count removed.
	/// </summary>
	public static int RemoveWhere<T>(this ICollection<T> collection, Func<T, bool> predicate)
	{
		ArgumentNullException.ThrowIfNull(collection);
		ArgumentNullException.ThrowIfNull(predicate);

		return InLock(collection, () =>
		{
			var matched = collection.Where(predicate).ToArray();

			foreach (var item in matched)
				collection.Remove(item);

			return matched.Length;
		});
	}

	/// <summary>
	/// Snapshot every item and clear the collection, atomically.
	/// </summary>
	public static T[] CopyAndClear<T>(this ICollection<T> collection)
	{
		ArgumentNullException.ThrowIfNull(collection);

		return InLock(collection, () =>
		{
			var arr = collection.ToArray();
			collection.Clear();
			return arr;
		});
	}

	#endregion

	#region SyncGet / SyncDo

	/// <summary>
	/// Run <paramref name="func"/> against the collection while its lock is held, and return
	/// the result. Use to make several reads (or a read-then-mutate) atomic with respect to
	/// other threads.
	/// </summary>
	public static TResult SyncGet<TCollection, TResult>(this TCollection collection, Func<TCollection, TResult> func)
		where TCollection : ISynchronized
	{
		ArgumentNullException.ThrowIfNull(collection);
		ArgumentNullException.ThrowIfNull(func);

		using (collection.SyncRoot.EnterScope())
			return func(collection);
	}

	/// <summary>
	/// Run <paramref name="action"/> against the collection while its lock is held. Use to make
	/// several mutations atomic with respect to other threads.
	/// </summary>
	public static void SyncDo<TCollection>(this TCollection collection, Action<TCollection> action)
		where TCollection : ISynchronized
	{
		ArgumentNullException.ThrowIfNull(collection);
		ArgumentNullException.ThrowIfNull(action);

		using (collection.SyncRoot.EnterScope())
			action(collection);
	}

	#endregion
}
