namespace StockSharp.Tests;

/// <summary>
/// Characterization tests for the synchronized-collection contract the codebase relies on.
///
/// These pin the OBSERVABLE behaviour of the collection types used throughout the core
/// (roughly 200 call sites, largely on the market-data path). They are written against the
/// current implementation so that an implementation swap has an acceptance bar: the same
/// tests must stay green afterwards. Nothing here is derived from the current provider's
/// source - only from how this repository uses these types and from standard collection
/// semantics.
///
/// The behaviour that matters most, and is easiest to get wrong in a replacement, is the
/// snapshot contract of the "Cached" variants: `Cache`/`CachedKeys`/`CachedValues`/
/// `CachedPairs` must hand back a stable point-in-time copy that callers can enumerate
/// without holding a lock, and that copy must not observe later mutations - while a fresh
/// access after a mutation must observe it.
/// </summary>
[TestClass]
public class SynchronizedCollectionContractTests : BaseTestClass
{
	#region SynchronizedDictionary

	[TestMethod]
	public void Dict_BasicOperations()
	{
		var dict = new SynchronizedDictionary<string, int>();

		dict.Count.AssertEqual(0);

		dict.Add("a", 1);
		dict["b"] = 2;

		dict.Count.AssertEqual(2);
		dict["a"].AssertEqual(1);
		dict.ContainsKey("b").AssertTrue();
		dict.ContainsKey("zzz").AssertFalse();

		dict.TryGetValue("a", out var v).AssertTrue();
		v.AssertEqual(1);

		dict.TryGetValue("zzz", out _).AssertFalse();

		dict.Remove("a").AssertTrue();
		dict.ContainsKey("a").AssertFalse();

		dict.Clear();
		dict.Count.AssertEqual(0);
	}

	[TestMethod]
	public void Dict_TryAdd_DoesNotOverwrite()
	{
		var dict = new SynchronizedDictionary<string, int>();

		dict.TryAdd2("k", 1).AssertTrue();
		dict.TryAdd2("k", 2).AssertFalse();

		dict["k"].AssertEqual(1);
	}

	[TestMethod]
	public void Dict_SafeAdd_CreatesOnceThenReturnsExisting()
	{
		var dict = new SynchronizedDictionary<string, List<int>>();
		var creations = 0;

		var first = dict.SafeAdd("k", _ => { creations++; return []; });
		var second = dict.SafeAdd("k", _ => { creations++; return []; });

		creations.AssertEqual(1);
		first.AssertSame(second);
		dict.Count.AssertEqual(1);
	}

	[TestMethod]
	public void Dict_TryGetAndRemove_RemovesAndReturns()
	{
		var dict = new SynchronizedDictionary<string, int> { { "k", 7 } };

		dict.TryGetAndRemove("k", out var got).AssertTrue();
		got.AssertEqual(7);
		dict.ContainsKey("k").AssertFalse();

		dict.TryGetAndRemove("k", out _).AssertFalse();
	}

	[TestMethod]
	public void Dict_ConcurrentWriters_NoLossNoCorruption()
	{
		var dict = new SynchronizedDictionary<int, int>();
		const int writers = 8;
		const int perWriter = 500;

		Parallel.For(0, writers, w =>
		{
			for (var i = 0; i < perWriter; i++)
				dict[w * perWriter + i] = i;
		});

		dict.Count.AssertEqual(writers * perWriter);
	}

	#endregion

	#region Cached snapshot contract

	[TestMethod]
	public void CachedDict_SnapshotIsStableAcrossLaterMutation()
	{
		var dict = new CachedSynchronizedDictionary<string, int>
		{
			{ "a", 1 },
			{ "b", 2 },
		};

		var keys = dict.CachedKeys;
		var values = dict.CachedValues;
		var pairs = dict.CachedPairs;

		keys.Length.AssertEqual(2);
		values.Length.AssertEqual(2);
		pairs.Length.AssertEqual(2);

		// mutating afterwards must not disturb the snapshot already handed out
		dict.Add("c", 3);
		dict.Remove("a");

		keys.Length.AssertEqual(2);
		values.Length.AssertEqual(2);
		pairs.Length.AssertEqual(2);
		keys.Contains("a").AssertTrue();
	}

	[TestMethod]
	public void CachedDict_FreshAccessObservesMutation()
	{
		var dict = new CachedSynchronizedDictionary<string, int> { { "a", 1 } };

		dict.CachedKeys.Length.AssertEqual(1);

		dict.Add("b", 2);
		dict.CachedKeys.Length.AssertEqual(2);

		dict.Remove("a");
		dict.CachedKeys.Length.AssertEqual(1);
		dict.CachedKeys.Contains("b").AssertTrue();

		dict.Clear();
		dict.CachedKeys.Length.AssertEqual(0);
	}

	[TestMethod]
	public void CachedSet_SnapshotIsStableAcrossLaterMutation()
	{
		var set = new CachedSynchronizedSet<string> { "a", "b" };

		var cache = set.Cache;
		cache.Length.AssertEqual(2);

		set.Add("c");
		set.Remove("a");

		cache.Length.AssertEqual(2);
		cache.Contains("a").AssertTrue();

		set.Cache.Length.AssertEqual(2);
		set.Cache.Contains("c").AssertTrue();
		set.Cache.Contains("a").AssertFalse();
	}

	[TestMethod]
	public void CachedSet_AddIsSetSemantics()
	{
		var set = new CachedSynchronizedSet<string>();

		set.Add("a").AssertTrue();
		set.Add("a").AssertFalse();

		set.Count.AssertEqual(1);
		set.Cache.Length.AssertEqual(1);
	}

	[TestMethod]
	public void CachedList_SnapshotIsStableAndOrdered()
	{
		var list = new CachedSynchronizedList<int> { 1, 2, 3 };

		var cache = list.Cache;
		cache.Length.AssertEqual(3);
		cache[0].AssertEqual(1);
		cache[2].AssertEqual(3);

		list.Add(4);

		cache.Length.AssertEqual(3);
		list.Cache.Length.AssertEqual(4);
	}

	[TestMethod]
	public void CachedCollections_EnumerateSnapshotWhileMutating()
	{
		// the reason these types exist: enumerate without holding a lock, while
		// another thread mutates, and neither side throws
		var set = new CachedSynchronizedSet<int>();

		for (var i = 0; i < 200; i++)
			set.Add(i);

		var mutator = Task.Run(() =>
		{
			for (var i = 200; i < 600; i++)
			{
				set.Add(i);
				set.Remove(i - 200);
			}
		});

		for (var round = 0; round < 50; round++)
		{
			var seen = 0;

			foreach (var _ in set.Cache)
				seen++;

			(seen >= 0).AssertTrue();
		}

		mutator.Wait();
	}

	#endregion

	#region SynchronizedList / Set / Queue

	[TestMethod]
	public void List_BasicOperationsAndOrder()
	{
		var list = new SynchronizedList<int> { 1, 2, 3 };

		list.Count.AssertEqual(3);
		list[1].AssertEqual(2);
		list.Contains(3).AssertTrue();

		list.Remove(2).AssertTrue();
		list.Count.AssertEqual(2);
		list[1].AssertEqual(3);

		list.Clear();
		list.Count.AssertEqual(0);
	}

	[TestMethod]
	public void Set_RejectsDuplicates()
	{
		var set = new SynchronizedSet<string> { "a" };

		set.Add("a").AssertFalse();
		set.Add("b").AssertTrue();
		set.Count.AssertEqual(2);
	}

	[TestMethod]
	public void Queue_IsFifo()
	{
		var queue = new SynchronizedQueue<int>();

		queue.Enqueue(1);
		queue.Enqueue(2);

		queue.Count.AssertEqual(2);
		queue.Dequeue().AssertEqual(1);
		queue.Dequeue().AssertEqual(2);
		queue.Count.AssertEqual(0);
	}

	#endregion
}
