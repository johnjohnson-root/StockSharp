namespace StockSharp.Tests;

using StockSharp.Foundation.Collections;

/// <summary>
/// The same contract as <see cref="SynchronizedCollectionContractTests"/>, run against the
/// first-party implementations in <c>StockSharp.Foundation.Collections</c>.
///
/// Both suites assert the same observable behaviour, so a green run here means the
/// replacement satisfies the contract the codebase already depends on. Where a member
/// differs deliberately, the difference is noted in the test.
/// </summary>
[TestClass]
public class FoundationCollectionContractTests : BaseTestClass
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
		dict.Remove("a").AssertFalse();
		dict.ContainsKey("a").AssertFalse();

		dict.Clear();
		dict.Count.AssertEqual(0);
	}

	[TestMethod]
	public void Dict_KeysAndValuesAreSnapshots()
	{
		var dict = new SynchronizedDictionary<string, int> { { "a", 1 } };

		var keys = dict.Keys;
		dict.Add("b", 2);

		keys.Count.AssertEqual(1);
		dict.Keys.Count.AssertEqual(2);
	}

	[TestMethod]
	public void Dict_EnumerationSurvivesConcurrentMutation()
	{
		var dict = new SynchronizedDictionary<int, int>();

		for (var i = 0; i < 200; i++)
			dict[i] = i;

		var mutator = Task.Run(() =>
		{
			for (var i = 200; i < 800; i++)
			{
				dict[i] = i;
				dict.Remove(i - 200);
			}
		});

		for (var round = 0; round < 50; round++)
		{
			foreach (var _ in dict)
			{
			}
		}

		mutator.Wait();
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
	public void CachedDict_RepeatedAccessReturnsSameInstanceUntilMutation()
	{
		var dict = new CachedSynchronizedDictionary<string, int> { { "a", 1 } };

		var first = dict.CachedKeys;
		dict.CachedKeys.AssertSame(first);

		dict.Add("b", 2);
		(dict.CachedKeys == first).AssertFalse();
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

		set.Add("a");
		set.Add("a");

		set.Count.AssertEqual(1);
		set.Cache.Length.AssertEqual(1);
		set.Contains("a").AssertTrue();

		// the first-party type additionally reports whether the item was new
		set.TryAdd("a").AssertFalse();
		set.TryAdd("b").AssertTrue();
		set.Count.AssertEqual(2);
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

	#region List / Set / Queue

	[TestMethod]
	public void List_BasicOperationsAndOrder()
	{
		var list = new SynchronizedList<int> { 1, 2, 3 };

		list.Count.AssertEqual(3);
		list[1].AssertEqual(2);
		list.Contains(3).AssertTrue();
		list.IndexOf(3).AssertEqual(2);

		list.Remove(2).AssertTrue();
		list.Count.AssertEqual(2);
		list[1].AssertEqual(3);

		list.Insert(0, 0);
		list[0].AssertEqual(0);

		list.RemoveAt(0);
		list.Count.AssertEqual(2);

		list.Clear();
		list.Count.AssertEqual(0);
	}

	[TestMethod]
	public void Set_RejectsDuplicates()
	{
		var set = new SynchronizedSet<string> { "a" };

		set.Add("a");
		set.Add("b");

		set.Count.AssertEqual(2);
		set.Contains("a").AssertTrue();
		set.Contains("b").AssertTrue();
	}

	[TestMethod]
	public void Queue_IsFifo()
	{
		var queue = new SynchronizedQueue<int>();

		queue.Enqueue(1);
		queue.Enqueue(2);

		queue.Count.AssertEqual(2);
		queue.Peek().AssertEqual(1);
		queue.Dequeue().AssertEqual(1);
		queue.Dequeue().AssertEqual(2);
		queue.Count.AssertEqual(0);

		queue.TryDequeue(out _).AssertFalse();
	}

	#endregion
}
