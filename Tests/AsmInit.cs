namespace StockSharp.Tests;

using Ecng.Compilation;
using Ecng.Compilation.Roslyn;
using Ecng.Excel;
using Ecng.Data;

using Microsoft.Data.SqlClient;

using StockSharp.Algo.Compilation;

[TestClass]
public static class AsmInit
{
	[AssemblyInitialize]
	public static async Task Init(TestContext _)
	{
		ConfigManager.RegisterService<ICompiler>(new CSharpCompiler());
		var secProvider = new CollectionSecurityProvider();
		ConfigManager.RegisterService<ISecurityProvider>(secProvider);
		ConfigManager.RegisterService<ISecurityStorage>(new InMemorySecurityStorage(secProvider));
		ConfigManager.RegisterService<IExchangeInfoProvider>(new InMemoryExchangeInfoProvider());
		ConfigManager.RegisterService<IExcelWorkerProvider>(new OpenXmlExcelWorkerProvider());
		ConfigManager.RegisterService<IMessageAdapterProvider>(new InMemoryMessageAdapterProvider([], typeof(MockRemoteAdapter)));
		await CompilationExtensions.Init(Paths.FileSystem, Helper.LogManager.Application, [("designer_extensions.py", File.ReadAllText("../../../../Diagram.Core/python/designer_extensions.py"))], default);

		ConfigManager.RegisterService<IDatabaseProvider>(new AdoDatabaseProvider());
		SqlServerDialect.Register(SqlClientFactory.Instance);

		Helper.FileSystem.ClearTemp();
	}

	[AssemblyCleanup]
	public static void UnInit()
	{
		Helper.FileSystem.ClearTemp();

		// left running, the log flusher keeps the test host process alive past the run
		Helper.LogManager.Dispose();

		// Post-run watchdog: anything still holding the test host alive after cleanup would
		// otherwise hit --blame-hang 8 minutes later, blamed on whichever test happened to run
		// last (see KNOWN-ISSUES.md). The thread is background, so it never delays a normal exit.
		var watchdog = new Thread(() =>
		{
			Thread.Sleep(TimeSpan.FromMinutes(3));

			Console.Error.WriteLine(
				"[AsmInit] Test host still alive 3 minutes after AssemblyCleanup " +
				$"(OS threads: {System.Diagnostics.Process.GetCurrentProcess().Threads.Count}). " +
				"Something leaked a foreground thread or blocked shutdown; exiting with code 97.");

			Environment.Exit(97);
		})
		{
			IsBackground = true,
			Name = "post-cleanup watchdog",
		};

		watchdog.Start();
	}
}