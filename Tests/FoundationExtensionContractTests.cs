namespace StockSharp.Tests;

using StockSharp.Foundation.Collections;

/// <summary>
/// Pins the contract of the synchronized-collection helper extensions
/// in <see cref="SynchronizedExtensions"/>.
/// </summary>
/// <remarks>
/// The semantics are reconstructed from this codebase's call sites, not from the previous
/// provider's source, so these tests are the authority a replacement implementation must satisfy.
/// </remarks>
[TestClass]
public class FoundationExtensionContractTests : BaseTestClass
{
	#region SafeAdd

	[TestMethod]
	public void SafeAdd_Factory_CreatesOnceThenReturnsExisting()
	{
		var dict = new SynchronizedDictionary<string, List<int>>();
		var calls = 0;

		var first = dict.SafeAdd("k", _ => { calls++; return []; });
		var second = dict.SafeAdd("k", _ => { calls++; return []; });

		calls.AssertEqual(1);
		first.AssertSame(second);
		dict.Count.AssertEqual(1);
	}

	[TestMethod]
	public void SafeAdd_Factory_ReceivesKey()
	{
		var dict = new SynchronizedDictionary<string, string>();

		var v = dict.SafeAdd("abc", k => k + "!");

		v.AssertEqual("abc!");
	}

	[TestMethod]
	public void SafeAdd_OutIsNew_ReportsCreationThenExisting()
	{
		var dict = new SynchronizedDictionary<string, int>();

		dict.SafeAdd("k", _ => 1, out var new1);
		dict.SafeAdd("k", _ => 2, out var new2);

		new1.AssertTrue();
		new2.AssertFalse();
		dict["k"].AssertEqual(1);
	}

	[TestMethod]
	public void SafeAdd_Parameterless_CreatesDefaultAndIsChainable()
	{
		var dict = new SynchronizedDictionary<string, List<int>>();

		// mirrors the codebase pattern: SafeAdd(key).Add(item)
		dict.SafeAdd("k").Add(7);
		dict.SafeAdd("k").Add(8);

		dict["k"].Count.AssertEqual(2);
		dict["k"][0].AssertEqual(7);
	}

	[TestMethod]
	public void SafeAdd_ParameterlessOutIsNew()
	{
		var dict = new SynchronizedDictionary<int, List<int>>();

		dict.SafeAdd(1, out var new1);
		dict.SafeAdd(1, out var new2);

		new1.AssertTrue();
		new2.AssertFalse();
	}

	#endregion

	#region TryAdd2 / TryGetAndRemove / GetAndRemove

	[TestMethod]
	public void TryAdd2_DoesNotOverwrite()
	{
		var dict = new SynchronizedDictionary<string, int>();

		dict.TryAdd2("k", 1).AssertTrue();
		dict.TryAdd2("k", 2).AssertFalse();

		dict["k"].AssertEqual(1);
	}

	[TestMethod]
	public void TryGetAndRemove_RemovesAndYields()
	{
		var dict = new SynchronizedDictionary<string, int> { { "k", 9 } };

		dict.TryGetAndRemove("k", out var v).AssertTrue();
		v.AssertEqual(9);
		dict.ContainsKey("k").AssertFalse();

		dict.TryGetAndRemove("k", out _).AssertFalse();
	}

	[TestMethod]
	public void GetAndRemove_ReturnsValueThenDefault()
	{
		var dict = new SynchronizedDictionary<string, int> { { "k", 5 } };

		dict.GetAndRemove("k").AssertEqual(5);
		dict.ContainsKey("k").AssertFalse();
		dict.GetAndRemove("k").AssertEqual(0);
	}

	#endregion

	#region RemoveWhere / RemoveRange / AddRange / CopyAndClear

	[TestMethod]
	public void Dict_RemoveWhere_RemovesMatchingReturnsCount()
	{
		var dict = new SynchronizedDictionary<int, int>
		{
			{ 1, 1 }, { 2, 2 }, { 3, 3 }, { 4, 4 },
		};

		var removed = dict.RemoveWhere(p => p.Value % 2 == 0);

		removed.AssertEqual(2);
		dict.Count.AssertEqual(2);
		dict.ContainsKey(1).AssertTrue();
		dict.ContainsKey(2).AssertFalse();
	}

	[TestMethod]
	public void Dict_CopyAndClear_SnapshotsThenEmpties()
	{
		var dict = new SynchronizedDictionary<string, int> { { "a", 1 }, { "b", 2 } };

		var snapshot = dict.CopyAndClear();

		snapshot.Length.AssertEqual(2);
		dict.Count.AssertEqual(0);
	}

	[TestMethod]
	public void Set_AddRange_RemoveRange_RemoveWhere()
	{
		var set = new CachedSynchronizedSet<int>();

		set.AddRange([1, 2, 3, 4, 5]);
		set.Count.AssertEqual(5);
		set.Cache.Length.AssertEqual(5);

		set.RemoveRange([2, 4, 99]).AssertEqual(2);
		set.Count.AssertEqual(3);

		set.RemoveWhere(x => x > 2).AssertEqual(2);
		set.Count.AssertEqual(1);
		set.Contains(1).AssertTrue();
	}

	[TestMethod]
	public void List_AddRange_CopyAndClear_PreserveOrder()
	{
		var list = new CachedSynchronizedList<int>();

		list.AddRange([1, 2, 3]);
		var snapshot = list.CopyAndClear();

		snapshot.Length.AssertEqual(3);
		snapshot[0].AssertEqual(1);
		snapshot[2].AssertEqual(3);
		list.Count.AssertEqual(0);
	}

	[TestMethod]
	public void Set_CopyAndClear_SnapshotStableAfterClear()
	{
		var set = new CachedSynchronizedSet<int> { 1, 2, 3 };

		var snapshot = set.CopyAndClear();

		snapshot.Length.AssertEqual(3);
		set.Count.AssertEqual(0);
	}

	#endregion

	#region SyncGet / SyncDo

	[TestMethod]
	public void SyncGet_ReturnsUnderLock_AndNestedHelperReenters()
	{
		var set = new CachedSynchronizedSet<int> { 1, 2, 3 };

		// the codebase pattern SyncGet(c => c.CopyAndClear()): the inner helper re-enters the
		// same lock, which must neither deadlock nor break atomicity
		var drained = set.SyncGet(c => c.CopyAndClear());

		drained.Length.AssertEqual(3);
		set.Count.AssertEqual(0);
	}

	[TestMethod]
	public void SyncDo_RunsUnderLock()
	{
		var dict = new SynchronizedDictionary<int, int> { { 1, 1 }, { 2, 2 }, { 3, 3 } };

		dict.SyncDo(d => d.RemoveWhere(p => p.Key >= 2));

		dict.Count.AssertEqual(1);
		dict.ContainsKey(1).AssertTrue();
	}

	[TestMethod]
	public void SyncDo_MultipleMutationsAreAtomicToObservers()
	{
		var set = new CachedSynchronizedSet<int>();

		for (var i = 0; i < 100; i++)
			set.Add(i);

		// an observer sampling Count during a swap must never see the intermediate (emptied)
		// state: SyncDo holds the lock across both operations, and Count takes the same lock
		var observer = Task.Run(() =>
		{
			for (var i = 0; i < 2000; i++)
				(set.Count is 100 or 5).AssertTrue();
		});

		for (var round = 0; round < 200; round++)
		{
			set.SyncDo(s =>
			{
				var items = s.CopyAndClear();
				foreach (var x in items.Take(5))
					s.Add(x);
			});

			set.SyncDo(s =>
			{
				var cur = s.CopyAndClear();
				for (var i = 0; i < 100; i++)
					s.Add(i);
			});
		}

		observer.Wait();
	}

	#endregion
}
