namespace StockSharp.Foundation.Collections;

/// <summary>
/// Collection helpers the codebase relies on alongside the synchronized collections.
/// </summary>
public static class CollectionExtensions
{
	/// <summary>
	/// Invoke an action for every item.
	/// </summary>
	/// <typeparam name="T">Item type.</typeparam>
	/// <param name="source">Items.</param>
	/// <param name="action">Action to invoke.</param>
	public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
	{
		ArgumentNullException.ThrowIfNull(source);
		ArgumentNullException.ThrowIfNull(action);

		foreach (var item in source)
			action(item);
	}

	/// <summary>
	/// First item, or <see langword="null"/> when the sequence is empty.
	/// </summary>
	/// <typeparam name="T">Item type.</typeparam>
	/// <param name="source">Items.</param>
	/// <returns>First item or <see langword="null"/>.</returns>
	public static T? FirstOr<T>(this IEnumerable<T> source)
		where T : struct
	{
		ArgumentNullException.ThrowIfNull(source);

		foreach (var item in source)
			return item;

		return null;
	}

	/// <summary>
	/// Value for the key, or the type default when the key is absent.
	/// </summary>
	/// <typeparam name="TKey">Key type.</typeparam>
	/// <typeparam name="TValue">Value type.</typeparam>
	/// <param name="dict">Dictionary.</param>
	/// <param name="key">Key.</param>
	/// <returns>Value or default.</returns>
	public static TValue TryGetValue<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key)
	{
		ArgumentNullException.ThrowIfNull(dict);

		return dict.TryGetValue(key, out var value) ? value : default;
	}
}
