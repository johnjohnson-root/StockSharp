# The consumed Ecng surface

This document inventories the `Ecng.*` API the fork actually uses.
It reads the pins in `Directory.Build.targets`,
the `PackageReference` lines in every project,
and the compiled Release assemblies of the 23 library projects
plus the five samples that carried a prior Release build,
and it writes one section per package
carrying the version, the referencing projects, the consumed types and members,
a size class for replacement, and the package's own Ecng dependencies.
Decision record `0003` orders the clean-room replacement from this data,
and TODO item `T2` consumes the ranking at the end.

Measured 2026-08-02 against the working tree at that date.
The `Completed passes` section below records each replacement since,
and the counts throughout carry those passes.

## What the numbers mean

A **consumed type** is one distinct `Ecng.*` entry in a fork assembly's `TypeRef` table.
A **consumed member** is one distinct `(declaring type, name, signature)` triple
in a fork assembly's `MemberRef` table.
Overloads count separately;
generic instantiations of one method collapse into one entry,
because the metadata signature carries placeholders rather than type arguments.

The **size class** follows the consumed member count:
trivial under 20, moderate from 20 through 100, heavy above 100.

**Public exposure** counts the package's types that appear in the fork's own public surface —
base types, implemented interfaces, and public or protected member signatures.
A package with zero public exposure is replaceable inside 1.x,
because no consumer of `StockSharp.*` can observe the change.
A package with public exposure reaches the drop-in contract that decision record `0006` holds through 1.x,
so its replacement carries either a type-forwarding shim or a `for 2.0` decision record.

Totals across the whole closure: **27 packages, 279 distinct types, 968 distinct members.**

## Method

The measurement walks IL metadata rather than source text.
A small `System.Reflection.Metadata` program opens each fork assembly,
resolves every `TypeRef` and `MemberRef` to its defining assembly,
and keeps the entries whose scope is an Ecng assembly.
Extension-method call sites resolve to ordinary `MemberRef` rows,
so `AddInfoLog` and `SafeAdd` land in the count exactly like instance calls.
Generic instantiations arrive as `TypeSpec` parents and decode back to their generic definition.

Two Ecng packages ship assemblies whose names leave the `Ecng.` prefix,
and the walk names both explicitly:
`Ecng.GeneticSharp` ships `GeneticSharp.dll`,
and `Ecng.UnitTesting` ships `MSTest.TestFramework.Extensions.dll` beside its own.

Four things stay outside metadata's reach,
and each one biases the count downward:

- Enum constants and `const` fields compile to literals,
  so a package used only for its enumeration values shows types with no members.
  `Ecng.Drawing` is the clearest case.
- Attribute named arguments live in the custom-attribute value blob rather than in a `MemberRef`,
  so only the attribute constructor appears.
- Reflection and string-driven lookup leave no reference at all.
- Samples compile outside both solutions,
  and only five sample assemblies carried a prior Release build,
  so the sample surface below covers `Storage.Local`, `Storage.Random`,
  `Strategies.HistoryBollingerBands`, `Testing.HistoryConsole`, and `Misc.Unit` alone.
  A `grep` pass over the other sample projects finds `Ecng.Xaml`,
  which arrives through the proprietary `StockSharp.Xaml.*` packages
  and sits outside the pinned set entirely.

The Ecng-internal dependency graph rests on the same evidence:
each Ecng assembly's own `AssemblyRef` table, read from `~/.nuget/packages/`.
Nuspec declarations agree with it everywhere except `Ecng.Compilation.All`,
which declares four package dependencies and references no assembly at all.

## The pinned set

`common_versions.props` sets `EcngVer` to `1.0.*`,
each project writes `Version="$(EcngVer)"`,
and `Directory.Build.targets` overrides every one with an exact version through `PackageReference Update`.
`common_target_tests.props` adds `Ecng.UnitTesting` to every test project.

Twenty `Ecng.*` ids carry a pin.
Seven more arrive transitively and land in the restore closure all the same,
which makes 27 packages the fork must own before the layer is its own.
The offline mirror in `nuget-mirror/` holds all 27.
`Ecng.Interop` reaches it through the samples solution:
its only consumer is `Samples/07_Testing/04_HistoryConsole`,
so `tools/mirror-packages.sh` restores `StockSharp_Samples.slnx`
beside the library and test solutions for exactly that reason,
and the mirror it produces carries 144 packages in all.

| package | version | pin | types | members | class | projects |
| --- | --- | --- | --- | --- | --- | --- |
| Ecng.Collections | 1.0.298 | transitive | 36 | 205 | heavy | 19 |
| Ecng.Common | 1.0.261 | transitive | 53 | 258 | heavy | 24 |
| Ecng.Compilation | 1.0.319 | pinned | 15 | 31 | moderate | 6 |
| Ecng.Compilation.FSharp | 1.0.207 | pinned | 1 | 1 | trivial | 1 |
| Ecng.Compilation.Python | 1.0.213 | pinned | 2 | 2 | trivial | 1 |
| Ecng.Compilation.Roslyn | 1.0.317 | pinned | 2 | 2 | trivial | 3 |
| Ecng.ComponentModel | 1.0.454 | pinned | 73 | 152 | heavy | 16 |
| Ecng.Configuration | 1.0.267 | pinned | 1 | 4 | trivial | 10 |
| Ecng.Data | 1.0.461 | pinned | 6 | 10 | trivial | 2 |
| Ecng.Data.Ado | 1.0.73 | pinned | 1 | 1 | trivial | 1 |
| Ecng.Data.SqlServer | 1.0.42 | pinned | 1 | 1 | trivial | 1 |
| Ecng.Drawing | 1.0.180 | pinned | 6 | 1 | trivial | 8 |
| Ecng.Excel | 1.0.24 | pinned | 2 | 19 | trivial | 5 |
| Ecng.Excel.OpenXml | 1.0.27 | pinned | 1 | 2 | trivial | 2 |
| Ecng.GeneticSharp | 1.0.3 | pinned | 29 | 30 | moderate | 2 |
| Ecng.Interop | 1.0.331 | pinned | 1 | 1 | trivial | 1 |
| Ecng.IO | 1.0.276 | transitive | 11 | 63 | moderate | 11 |
| Ecng.IO.Compression | 1.0.40 | pinned | 1 | 2 | trivial | 1 |
| Ecng.Linq | 1.0.177 | pinned | 2 | 2 | trivial | 4 |
| Ecng.Localization | 1.0.256 | transitive | 2 | 1 | trivial | 1 |
| Ecng.Logging | 1.0.181 | pinned | 12 | 59 | moderate | 17 |
| Ecng.Net | 1.0.527 | pinned | 1 | 1 | trivial | 1 |
| Ecng.Reflection | 1.0.311 | transitive | 2 | 10 | trivial | 14 |
| Ecng.Security | 1.0.314 | transitive | 2 | 3 | trivial | 3 |
| Ecng.Serialization | 1.0.359 | transitive | 11 | 53 | moderate | 14 |
| Ecng.StringSearch | 1.0.255 | pinned | 2 | 6 | trivial | 1 |
| Ecng.UnitTesting | 1.0.287 | pinned | 3 | 48 | moderate | 1 |

## StockSharp.Samples.HistoryData

Version 5.0.3, pinned in `Directory.Build.targets` beside the Ecng ids.
The package carries 260 `.bin` market-data files under `HistoryData/` and no assembly,
so it contributes zero API surface and sits outside the clean-room program.
`Tests` consumes it, as do roughly twenty sample projects.
Replacing it means sourcing equivalent historical data,
which is a data-licensing question rather than an engineering one.

## Packages

Sections run smallest consumed surface first within each size class.

### Ecng.Compilation.FSharp

Version 1.0.207, pinned directly since the wave 1 rank 1 pass.
Ships `Ecng.Compilation.FSharp.dll`; the fork binds 1 of its 1 public type and 1 of its 1 public member.
Size class: trivial.
Referencing projects: `Algo.Compilation`.
Ecng-internal dependencies: `Ecng.Collections`, `Ecng.Common`, `Ecng.Compilation`, `Ecng.Security`.
Ecng-internal dependents: none.
Public exposure: none.

    Ecng.Compilation.FSharp.FSharpCompiler
        .ctor () -> Void

A replacement implements `Ecng.Compilation.ICompiler`,
so it keeps binding `Ecng.Compilation` until that package falls too.

### Ecng.Data.Ado

Version 1.0.73, pinned directly.
Ships `Ecng.Data.Ado.dll`; the fork binds 1 of its 1 public type and 1 of its 6 public members.
Size class: trivial.
Referencing projects: `Tests`.
Ecng-internal dependencies: `Ecng.Common`, `Ecng.Data`.
Ecng-internal dependents: none.
Public exposure: none.

    Ecng.Data.AdoDatabaseProvider
        .ctor () -> Void

The single reference registers the provider into `Ecng.Data`'s registry,
so the replacement implements `Ecng.Data.IDatabaseProvider` and moves with `Ecng.Data`.

### Ecng.Data.SqlServer

Version 1.0.42, pinned directly.
Ships `Ecng.Data.SqlServer.dll`; the fork binds 1 of its 1 public type and 1 of its 30 public members.
Size class: trivial.
Referencing projects: `Tests`.
Ecng-internal dependencies: `Ecng.Common`, `Ecng.Data`.
Ecng-internal dependents: none.
Public exposure: none.

    Ecng.Data.SqlServerDialect
        Register (System.Data.Common.DbProviderFactory) -> Void

### Ecng.Drawing

Version 1.0.180, pinned directly.
Ships `Ecng.Drawing.dll`; the fork binds 6 of its 8 public types and 1 of its 41 public members.
Size class: trivial.
Referencing projects: `Algo.Analytics`, `Algo.Analytics.CSharp`, `Algo.Analytics.FSharp`,
`Algo.Indicators`, `BusinessEntities`, `Charting.Interfaces`, `Tests`,
and the `Strategies.HistoryBollingerBands` sample.
Ecng-internal dependencies: `Ecng.Common`, `Ecng.Localization`.
Ecng-internal dependents: none.
Public exposure: 5 types — `Brush`, `DrawStyles`, `HorizontalAlignment`, `Thickness`, `VerticalAlignment`.

    Ecng.Drawing.DrawingExtensions
        ToColor (Int32) -> System.Drawing.Color

    types with no member reference
        Ecng.Drawing.Brush
        Ecng.Drawing.DrawStyles
        Ecng.Drawing.HorizontalAlignment
        Ecng.Drawing.Thickness
        Ecng.Drawing.VerticalAlignment

The one member hides the real weight.
`DrawStyles` is an enumeration whose constants compile to literals,
and the five value types appear in fork-public signatures such as `IIndicator.Style` and `IAnnotationData.Stroke`.
Replacing this package rewrites the shape of the fork's own charting contract.

### Ecng.Interop

Version 1.0.331, pinned directly.
Ships `Ecng.Interop.dll`; the fork binds 1 of its 195 public types and 1 of its 820 public members.
Size class: trivial.
Referencing projects: the `Testing.HistoryConsole` sample alone.
Ecng-internal dependencies: `Ecng.Common`.
Ecng-internal dependents: none.
Public exposure: none.

    Ecng.Interop.ProcessExtensions
        OpenLink (String, Boolean) -> Boolean

The widest package in the closure carries the thinnest consumption:
one call opening a report file at `Samples/07_Testing/04_HistoryConsole/Program.cs`.
That single call is why `tools/mirror-packages.sh` restores the samples
solution as well as the library and test ones:
no project in either of those two touches the package,
so a mirror built from them alone would omit a pinned id.

### Ecng.Localization

Version 1.0.256, transitive through `Ecng.ComponentModel`, `Ecng.Drawing`, `Ecng.Logging`, `Ecng.Net`, and `Ecng.Compilation.Roslyn`.
Ships `Ecng.Localization.dll`; the fork binds 2 of its 2 public types and 1 of its 86 public members.
Size class: trivial.
Referencing projects: `Localization`.
Ecng-internal dependencies: none.
Ecng-internal dependents: `Ecng.ComponentModel`, `Ecng.Compilation.Roslyn`, `Ecng.Drawing`, `Ecng.Logging`, `Ecng.Net`.
Public exposure: none.

    Ecng.Localization.LocalizedStrings
        set_Localizer (Ecng.Localization.ILocalizer) -> Void

    types with no member reference
        Ecng.Localization.ILocalizer

`Localization/LocalizedStrings.cs` implements `ILocalizer` and installs it once.
Five Ecng packages hold this one in place,
so its pin falls late despite the tiny surface.

### Ecng.Net

Version 1.0.527, pinned directly.
Ships `Ecng.Net.dll`; the fork binds 1 of its 19 public types and 1 of its 165 public members.
Size class: trivial.
Referencing projects: `Algo`.
Ecng-internal dependencies: `Ecng.Collections`, `Ecng.Common`, `Ecng.ComponentModel`, `Ecng.Localization`, `Ecng.Serialization`.
Ecng-internal dependents: none.
Public exposure: none.

    Ecng.Net.NetworkHelper
        IsNetworkPath (String) -> Boolean

One predicate at `Algo/Storages/DriveCache.cs` holds a 165-member package in the graph.

### Ecng.Compilation.Python

Version 1.0.213, pinned directly since the wave 1 rank 1 pass.
Ships `Ecng.Compilation.Python.dll`; the fork binds 2 of its 2 public types and 2 of its 16 public members.
Size class: trivial.
Referencing projects: `Algo.Compilation`.
Ecng-internal dependencies: `Ecng.Collections`, `Ecng.Common`, `Ecng.Compilation`, `Ecng.ComponentModel`, `Ecng.Reflection`.
Ecng-internal dependents: none.
Public exposure: none.

    Ecng.Compilation.Python.PythonCompiler
        .ctor (Microsoft.Scripting.Hosting.ScriptEngine) -> Void
    Ecng.Compilation.Python.PythonExtensions
        IsPythonObject (Object) -> Boolean

### Ecng.Compilation.Roslyn

Version 1.0.317, pinned directly.
Ships `Ecng.Compilation.Roslyn.dll`; the fork binds 2 of its 3 public types and 2 of its 15 public members.
Size class: trivial.
Referencing projects: `Algo.Compilation`, `Tests`, and the `Storage.Local` sample.
Six more sample projects name the package in their csproj.
Ecng-internal dependencies: `Ecng.Common`, `Ecng.Compilation`, `Ecng.Localization`.
Ecng-internal dependents: none.
Public exposure: none.

    Ecng.Compilation.Roslyn.CSharpCompiler
        .ctor () -> Void
    Ecng.Compilation.Roslyn.VisualBasicCompiler
        .ctor () -> Void

The consumed surface is two constructors,
and the implementation behind them is a full Roslyn hosting layer.
A replacement rebuilds that layer against `Microsoft.CodeAnalysis` directly.

### Ecng.Excel.OpenXml

Version 1.0.27, pinned directly.
Ships `Ecng.Excel.OpenXml.dll`; the fork binds 1 of its 1 public type and 2 of its 3 public members.
Size class: trivial.
Referencing projects: `Tests` and the `Testing.HistoryConsole` sample.
Ecng-internal dependencies: `Ecng.Common`, `Ecng.Excel`.
Ecng-internal dependents: none.
Public exposure: none.

    Ecng.Excel.OpenXmlExcelWorkerProvider
        .ctor () -> Void
        OpenExist (System.IO.Stream) -> Ecng.Excel.IExcelWorker

The provider returns `Ecng.Excel.IExcelWorker`,
so this package and `Ecng.Excel` replace together.

### Ecng.IO.Compression

Version 1.0.40, pinned directly.
Ships `Ecng.IO.Compression.dll`; the fork binds 1 of its 4 public types and 2 of its 62 public members.
Size class: trivial.
Referencing projects: `Algo`.
Ecng-internal dependencies: `Ecng.Common`, `Ecng.IO`.
Ecng-internal dependents: none.
Public exposure: none.

    Ecng.IO.Compression.CompressionHelper
        Compress (Byte[], System.Nullable`1<Int32>, System.Nullable`1<Int32>, System.IO.Compression.CompressionLevel, Int32) -> Byte[]
        Uncompress (Byte[], System.Nullable`1<Int32>, System.Nullable`1<Int32>, Int32) -> Byte[]

Both signatures name framework types alone,
so the replacement binds no Ecng type.

### Ecng.Linq

Version 1.0.177, pinned directly.
Ships `Ecng.Linq.dll`; the fork binds 2 of its 4 public types and 2 of its 32 public members.
Size class: trivial.
Referencing projects: `Algo`, `Algo.Export`, `Algo.Testing`, `Tests`.
Ecng-internal dependencies: `Ecng.Common`.
Ecng-internal dependents: none.
Public exposure: none.

    Ecng.Linq.AsyncEnumerableExtensions
        Cast (System.Collections.Generic.IAsyncEnumerable`1<!!0>, System.Func`2<!!0,!!1>) -> System.Collections.Generic.IAsyncEnumerable`1<!!1>
    Ecng.Linq.SyncAsyncEnumerable`1
        .ctor (System.Collections.Generic.IEnumerable`1<!0>) -> Void

### Ecng.Security

Version 1.0.314, transitive through `Ecng.Serialization` and `Ecng.ComponentModel`.
Ships `Ecng.Security.dll`; the fork binds 2 of its 12 public types and 3 of its 71 public members.
Size class: trivial.
Referencing projects: `Configuration`, `Diagram.Core`, `Tests`.
Ecng-internal dependencies: `Ecng.Collections`, `Ecng.Common`, `Ecng.IO`.
Ecng-internal dependents: `Ecng.Compilation`, `Ecng.Compilation.FSharp`, `Ecng.ComponentModel`, `Ecng.Serialization`.
Public exposure: 1 type — `IAuthorization`.

    Ecng.Security.CryptoHelper
        Decrypt (Byte[], String, Byte[], Byte[]) -> Byte[]
        Encrypt (Byte[], String, Byte[], Byte[]) -> Byte[]
    Ecng.Security.IAuthorization
        ValidateCredentials (String, System.Security.SecureString, System.Net.IPAddress, System.Threading.CancellationToken) -> System.Threading.Tasks.ValueTask`1<String>

### Ecng.Configuration

Version 1.0.267, pinned directly.
Ships `Ecng.Configuration.dll`; the fork binds 1 of its 1 public type and 4 of its 25 public members.
Size class: trivial.
Referencing projects: `Alerts.Interfaces`, `Algo`, `Algo.Compilation`, `Algo.Export`,
`BusinessEntities`, `Charting.Interfaces`, `Configuration`, `Diagram.Core`, `Tests`,
and the `Storage.Local` sample.
Ecng-internal dependencies: `Ecng.Common`.
Ecng-internal dependents: none.
Public exposure: none.

    Ecng.Configuration.ConfigManager
        GetService () -> !!0
        RegisterService (!!0) -> Void
        TryGet (String, !!0) -> !!0
        TryGetService () -> !!0

Four static members of one service-locator class,
reached from ten projects.
The wide fan-in raises the migration cost above what the member count suggests,
and the replacement itself is a day of work.

### Ecng.StringSearch

Version 1.0.255, pinned directly.
Ships `Ecng.StringSearch.dll`; the fork binds 2 of its 15 public types and 6 of its 117 public members.
Size class: trivial.
Referencing projects: `Algo`.
Ecng-internal dependencies: `Ecng.Collections`.
Ecng-internal dependents: none.
Public exposure: none.

    Gma.DataStructures.StringSearch.ITrie`1
        Add (String, !0) -> Void
        Clear () -> Void
        Remove (!0) -> Void
        RemoveRange (System.Collections.Generic.IEnumerable`1<!0>) -> Void
        Retrieve (String) -> System.Collections.Generic.IEnumerable`1<!0>
    Gma.DataStructures.StringSearch.PatriciaSuffixTrie`1
        .ctor (Int32) -> Void

One field in `Algo/SecurityTrie.cs` holds the whole package.
The types keep the upstream `Gma.DataStructures.StringSearch` namespace,
which marks the package as a repackaged third-party trie rather than Ecng-authored code.

### Ecng.Data

Version 1.0.461, pinned directly.
Ships `Ecng.Data.dll`; the fork binds 6 of its 16 public types and 10 of its 397 public members.
Size class: trivial.
Referencing projects: `Algo.Export`, `Tests`.
Ecng-internal dependencies: `Ecng.Collections`, `Ecng.Common`, `Ecng.ComponentModel`, `Ecng.Serialization`.
Ecng-internal dependents: `Ecng.Data.Ado`, `Ecng.Data.SqlServer`.
Public exposure: 3 types — `DatabaseConnectionCache`, `DatabaseConnectionPair`, `IDatabaseProvider`.

    Ecng.Data.DatabaseConnectionPair
        .ctor () -> Void
        set_ConnectionString (String) -> Void
        set_Provider (String) -> Void
    Ecng.Data.DatabaseProviderRegistry
        get_AllProviders () -> String[]
    Ecng.Data.IDatabaseProvider
        CreateConnection (Ecng.Data.DatabaseConnectionPair) -> Ecng.Data.IDatabaseConnection
        GetTable (Ecng.Data.IDatabaseConnection, String) -> Ecng.Data.IDatabaseTable
    Ecng.Data.IDatabaseTable
        BulkInsertAsync (System.Collections.Generic.IEnumerable`1<System.Collections.Generic.IDictionary`2<String,Object>>, System.Threading.CancellationToken) -> System.Threading.Tasks.Task
        CreateAsync (System.Collections.Generic.IDictionary`2<String,System.Type>, System.Threading.CancellationToken) -> System.Threading.Tasks.Task
        DropAsync (System.Threading.CancellationToken) -> System.Threading.Tasks.Task
        InsertAsync (System.Collections.Generic.IDictionary`2<String,Object>, System.Threading.CancellationToken) -> System.Threading.Tasks.Task

    types with no member reference
        Ecng.Data.DatabaseConnectionCache
        Ecng.Data.IDatabaseConnection

Ten members out of 397 makes this the thinnest slice of any moderate-sized package,
and the consumed part is four interfaces plus a connection-pair holder.

### Ecng.Reflection

Version 1.0.311, transitive through `Ecng.Serialization` and `Ecng.ComponentModel`.
Ships `Ecng.Reflection.dll`; the fork binds 2 of its 4 public types and 10 of its 82 public members.
Size class: trivial.
Referencing projects: `Alerts.Interfaces`, `Algo`, `Algo.Analytics`, `Algo.Analytics.CSharp`,
`Algo.Analytics.FSharp`, `Algo.Compilation`, `Algo.Gpu`, `Algo.Indicators`, `Algo.Strategies`,
`BusinessEntities`, `Configuration`, `Diagram.Core`, `Messages`, `Tests`.
Ecng-internal dependencies: `Ecng.Collections`, `Ecng.Common`.
Ecng-internal dependents: `Ecng.Compilation.Python`, `Ecng.ComponentModel`, `Ecng.Serialization`.
Public exposure: 1 type — `VoidType`.

    Ecng.Reflection.ReflectionHelper
        FindImplementations (System.Reflection.Assembly, Boolean, Boolean, Boolean, System.Func`2<System.Type,Boolean>) -> System.Collections.Generic.IEnumerable`1<System.Type>
        GetGenericType (System.Type, System.Type) -> System.Type
        GetMember (System.Type, String, System.Type[]) -> !!0
        GetMembers (System.Type, System.Reflection.BindingFlags, System.Type[]) -> !!0[]
        IsAssembly (String) -> Boolean
        IsCollection (System.Type) -> Boolean
        IsModifiable (System.Reflection.PropertyInfo) -> Boolean
        IsRequiredType (System.Type) -> Boolean
        Make (System.Reflection.MethodInfo, System.Type[]) -> System.Reflection.MethodInfo
        TryFindType (System.Collections.Generic.IEnumerable`1<System.Type>, System.Func`2<System.Type,Boolean>, String) -> System.Type

    types with no member reference
        Ecng.Reflection.VoidType

Fourteen projects reach ten members,
which makes this a small implementation with a wide migration.

### Ecng.Excel

Version 1.0.24, pinned directly.
Ships `Ecng.Excel.dll`; the fork binds 2 of its 5 public types and 19 of its 94 public members.
Size class: trivial.
Referencing projects: `Algo`, `Algo.Export`, `Reporting`, `Tests`,
and the `Testing.HistoryConsole` sample.
Ecng-internal dependencies: `Ecng.Common`.
Ecng-internal dependents: `Ecng.Excel.OpenXml`.
Public exposure: 1 type — `IExcelWorkerProvider`.

    Ecng.Excel.IExcelWorker
        AddLineChart (String, String, Int32, Int32, Int32, Int32, Int32, Int32) -> Ecng.Excel.IExcelWorker
        AddSheet () -> Ecng.Excel.IExcelWorker
        ContainsSheet (String) -> Boolean
        FreezeRows (Int32) -> Ecng.Excel.IExcelWorker
        GetCell (Int32, Int32) -> !!0
        GetRowsCount () -> Int32
        GetSheetNames () -> System.Collections.Generic.IEnumerable`1<String>
        MergeCells (Int32, Int32, Int32, Int32) -> Ecng.Excel.IExcelWorker
        RenameSheet (String) -> Ecng.Excel.IExcelWorker
        SetCell (Int32, Int32, !!0) -> Ecng.Excel.IExcelWorker
        SetCellColor (Int32, Int32, String, String) -> Ecng.Excel.IExcelWorker
        SetCellFormat (Int32, Int32, String) -> Ecng.Excel.IExcelWorker
        SetColorScale (Int32, Int32, String, String, String) -> Ecng.Excel.IExcelWorker
        SetColumnWidth (Int32, Double) -> Ecng.Excel.IExcelWorker
        SetStyle (Int32, String) -> Ecng.Excel.IExcelWorker
        SetStyle (Int32, System.Type) -> Ecng.Excel.IExcelWorker
        SwitchSheet (String) -> Ecng.Excel.IExcelWorker
    Ecng.Excel.IExcelWorkerProvider
        CreateNew (System.IO.Stream, Boolean) -> Ecng.Excel.IExcelWorker
        OpenExist (System.IO.Stream) -> Ecng.Excel.IExcelWorker

Two interfaces, no implementation types.
The implementation the fork actually runs lives in `Ecng.Excel.OpenXml`,
so the pair carries 21 members between them.

### Ecng.GeneticSharp

Version 1.0.3, pinned directly.
Ships `GeneticSharp.dll`; the fork binds 29 of its 102 public types and 30 of its 500 public members.
Size class: moderate.
Referencing projects: `Algo.Strategies`, `Tests`.
Ecng-internal dependencies: none.
Ecng-internal dependents: none.
Public exposure: 3 types — `ICrossover`, `IMutation`, `ISelection`.

    GeneticSharp.ChromosomeBase
        .ctor (Int32) -> Void
        GenerateGene (Int32) -> GeneticSharp.Gene
        GetGenes () -> GeneticSharp.Gene[]
        ReplaceGene (Int32, GeneticSharp.Gene) -> Void
        get_Length () -> Int32
    GeneticSharp.CrossoverService
        GetCrossoverTypes () -> System.Collections.Generic.IList`1<System.Type>
    GeneticSharp.FitnessStagnationTermination
        .ctor (Int32) -> Void
    GeneticSharp.Gene
        .ctor (Object) -> Void
        get_Value () -> Object
    GeneticSharp.GenerationNumberTermination
        .ctor (Int32) -> Void
    GeneticSharp.GeneticAlgorithm
        .ctor (GeneticSharp.IPopulation, GeneticSharp.IFitness, GeneticSharp.ISelection, GeneticSharp.ICrossover, GeneticSharp.IMutation) -> Void
        Start () -> Void
        Stop () -> Void
        set_CrossoverProbability (Single) -> Void
        set_MutationProbability (Single) -> Void
        set_Reinsertion (GeneticSharp.IReinsertion) -> Void
        set_TaskExecutor (GeneticSharp.ITaskExecutor) -> Void
        set_Termination (GeneticSharp.ITermination) -> Void
    GeneticSharp.IAsyncFitness
        EvaluateAsync (GeneticSharp.IChromosome, System.Threading.CancellationToken) -> System.Threading.Tasks.Task`1<Double>
    GeneticSharp.IFitness
        Evaluate (GeneticSharp.IChromosome) -> Double
    GeneticSharp.IMutation
        get_MinChromosomeLength () -> Int32
    GeneticSharp.MutationService
        GetMutationTypes () -> System.Collections.Generic.IList`1<System.Type>
    GeneticSharp.OrTermination
        .ctor (GeneticSharp.ITermination[]) -> Void
    GeneticSharp.ParallelTaskExecutor
        .ctor () -> Void
        set_MaxThreads (Int32) -> Void
        set_MinThreads (Int32) -> Void
    GeneticSharp.Population
        .ctor (Int32, Int32, GeneticSharp.IChromosome) -> Void
    GeneticSharp.ReinsertionService
        GetReinsertionTypes () -> System.Collections.Generic.IList`1<System.Type>
    GeneticSharp.SelectionService
        GetSelectionTypes () -> System.Collections.Generic.IList`1<System.Type>
    GeneticSharp.TerminationBase
        .ctor () -> Void

    types with no member reference
        GeneticSharp.ElitistReinsertion
        GeneticSharp.IChromosome
        GeneticSharp.ICrossover
        GeneticSharp.IGeneticAlgorithm
        GeneticSharp.IPopulation
        GeneticSharp.IReinsertion
        GeneticSharp.ISelection
        GeneticSharp.ITaskExecutor
        GeneticSharp.ITermination
        GeneticSharp.OnePointCrossover
        GeneticSharp.SequenceMutationBase
        GeneticSharp.TournamentSelection
        GeneticSharp.UniformMutation

`Ecng.GeneticSharp` is a rebranded fork of the MIT-licensed GeneticSharp library,
and the assembly keeps the original name and namespace.
`Algo.Strategies/Optimization/GeneticOptimizer.cs` is the sole consumer,
and TODO item `T10` already schedules work in that file.
A replacement here has an option the other packages lack:
take the upstream MIT package directly rather than reimplement 30 members.

### Ecng.Compilation

Version 1.0.319, pinned directly.
Ships `Ecng.Compilation.dll`; the fork binds 15 of its 19 public types and 31 of its 124 public members.
Size class: moderate.
Referencing projects: `Algo`, `Algo.Compilation`, `Algo.Strategies`, `Diagram.Core`, `Tests`,
and the `Storage.Local` sample.
Ecng-internal dependencies: `Ecng.Collections`, `Ecng.Common`, `Ecng.IO`, `Ecng.Security`, `Ecng.Serialization`.
Ecng-internal dependents: `Ecng.Compilation.FSharp`, `Ecng.Compilation.Python`, `Ecng.Compilation.Roslyn`.
Public exposure: 9 types — `AssemblyLoadContextTracker`, `AssemblyReference`, `CompilationError`,
`CompilerProvider`, `Expressions.ExpressionFormula<T>`, `ICodeReference`, `ICompiler`,
`ICompilerCache`, `NuGetReference`.

Members by declaring type:

    5   Ecng.Compilation.Expressions.ExpressionFormula`1
    5   Ecng.Compilation.ICompiler
    4   Ecng.Compilation.ICompilerExtensions
    3   Ecng.Compilation.AssemblyReference
    3   Ecng.Compilation.ICompilerCache
    2   Ecng.Compilation.AssemblyLoadContextTracker
    2   Ecng.Compilation.CompilationResult
    2   Ecng.Compilation.ICodeReference
    1   Ecng.Compilation.AssemblyCompilationResult
    1   Ecng.Compilation.CompilationError
    1   Ecng.Compilation.CompilerProvider
    1   Ecng.Compilation.Expressions.ExpressionHelper
    1   Ecng.Compilation.ICompilerContext

    types with no member reference
        Ecng.Compilation.CompilationErrorTypes
        Ecng.Compilation.NuGetReference

The expression-formula path deserves separate attention:
`ExpressionHelper.Compile` takes an `Ecng.IO.IFileSystem` and an `ICompilerCache`,
so the compilation abstraction and the file-system abstraction move together.

### Ecng.UnitTesting

Version 1.0.287, pinned directly through `common_target_tests.props`.
Ships `Ecng.UnitTesting.dll` and `MSTest.TestFramework.Extensions.dll`;
the fork binds 3 of their 8 public types and 48 of their 153 public members.
Size class: moderate.
Referencing projects: `Tests`.
Ecng-internal dependencies: `Ecng.Common`, `Ecng.Serialization`.
Ecng-internal dependents: none.
Public exposure: 1 type — `BaseTestClass`, and `StockSharp.Tests` ships to nobody.

Members by declaring type:

    30  Ecng.UnitTesting.BaseTestClass
    17  Ecng.UnitTesting.AssertHelper
    1   Microsoft.VisualStudio.TestTools.UnitTesting.TestContext (get_CancellationToken)

`BaseTestClass` supplies the assertion vocabulary the whole 4458-test suite writes in —
`AssertEqual`, `AssertTrue`, `IsInRange`, `Throws`, `ThrowsAsync`, `HasCount`, `GetSecret` —
and a replacement is a mechanical port over MSTest's own `Assert`.
The surface is bounded, the consumer count is one project,
and nothing outside the repository observes the result.

### Ecng.Serialization

Version 1.0.359, transitive through `Ecng.ComponentModel`, `Ecng.Logging`, `Ecng.Compilation`, `Ecng.Data`, `Ecng.Net`, and `Ecng.UnitTesting`.
Ships `Ecng.Serialization.dll`; the fork binds 11 of its 27 public types and 53 of its 264 public members.
Size class: moderate.
Referencing projects: `Alerts.Interfaces`, `Algo`, `Algo.Export`, `Algo.Import`, `Algo.Indicators`,
`Algo.Strategies`, `Algo.Testing`, `BusinessEntities`, `Charting.Interfaces`, `Configuration`,
`Diagram.Core`, `Localization`, `Messages`, `Tests`.
Ecng-internal dependencies: `Ecng.Collections`, `Ecng.Common`, `Ecng.IO`, `Ecng.Reflection`, `Ecng.Security`.
Ecng-internal dependents: `Ecng.Compilation`, `Ecng.ComponentModel`, `Ecng.Data`, `Ecng.Logging`, `Ecng.Net`, `Ecng.UnitTesting`.
Public exposure: 3 types — `IPersistable`, `ISerializer<T>`, `SettingsStorage`.

Members by declaring type:

    14  Ecng.Serialization.PersistableHelper
    9   Ecng.Serialization.SpanWriter
    8   Ecng.Serialization.SpanReader
    6   Ecng.Serialization.SettingsStorage
    5   Ecng.Serialization.JsonSerializer`1
    3   Ecng.Serialization.ISerializerExtensions
    3   Ecng.Serialization.JsonHelper
    2   Ecng.Serialization.ContinueOnExceptionContext
    2   Ecng.Serialization.IPersistable
    1   Ecng.Serialization.ISerializer`1

    types with no member reference
        Ecng.Serialization.ISerializer

The package splits cleanly into three independent pieces:
the `IPersistable` / `SettingsStorage` persistence protocol at 22 members,
the `SpanReader` / `SpanWriter` binary codec at 17,
and the Newtonsoft-backed JSON serializer at 11.
`IPersistable` and `SettingsStorage` sit in the fork's public API,
so the persistence piece carries a contract decision the other two avoid.

### Ecng.Logging

Version 1.0.181, pinned directly.
Ships `Ecng.Logging.dll`; the fork binds 12 of its 26 public types and 59 of its 240 public members.
Size class: moderate.
Referencing projects: `Alerts.Interfaces`, `Algo`, `Algo.Analytics`, `Algo.Analytics.CSharp`,
`Algo.Analytics.FSharp`, `Algo.Compilation`, `Algo.Import`, `Algo.Indicators`, `Algo.Strategies`,
`Algo.Testing`, `BusinessEntities`, `Configuration`, `Diagram.Core`, `Messages`, `Tests`,
and the `Strategies.HistoryBollingerBands` and `Testing.HistoryConsole` samples.
Ecng-internal dependencies: `Ecng.Collections`, `Ecng.Common`, `Ecng.ComponentModel`, `Ecng.IO`, `Ecng.Localization`, `Ecng.Serialization`.
Ecng-internal dependents: none.
Public exposure: 6 types — `BaseLogReceiver`, `ILogReceiver`, `ILogSource`, `LogLevels`, `LogManager`, `LogMessage`.

Members by declaring type:

    13  Ecng.Logging.BaseLogSource
    11  Ecng.Logging.ILogSource
    10  Ecng.Logging.LoggingHelper
    7   Ecng.Logging.BaseLogReceiver
    6   Ecng.Logging.LogManager
    5   Ecng.Logging.ILogReceiver
    4   Ecng.Logging.LogMessage
    1   Ecng.Logging.ConsoleLogListener
    1   Ecng.Logging.FileLogListener
    1   Ecng.Logging.LogReceiver

    types with no member reference
        Ecng.Logging.ILogListener
        Ecng.Logging.LogLevels

The largest leaf in the graph:
nothing in Ecng depends on it,
and 17 fork projects do.
`ILogSource` and `ILogReceiver` run through the fork's whole public object model —
every `Connector`, `Strategy`, and adapter implements one of them —
so a replacement lands as a `for 2.0` break or as a forwarding shim.

### Ecng.IO

Version 1.0.276, transitive through `Ecng.Compilation`, `Ecng.IO.Compression`, `Ecng.Logging`, `Ecng.Security`, and `Ecng.Serialization`.
Ships `Ecng.IO.dll`; the fork binds 11 of its 15 public types and 63 of its 238 public members.
Size class: moderate.
Referencing projects: `Algo`, `Algo.Compilation`, `Algo.Import`, `Algo.Strategies`, `Configuration`,
`Diagram.Core`, `Messages`, `Tests`,
and the `Storage.Local`, `Strategies.HistoryBollingerBands`, and `Testing.HistoryConsole` samples.
Ecng-internal dependencies: `Ecng.Common`.
Ecng-internal dependents: `Ecng.Compilation`, `Ecng.IO.Compression`, `Ecng.Logging`, `Ecng.Security`, `Ecng.Serialization`.
Public exposure: 3 types — `CsvFileWriter`, `FastCsvReader`, `IFileSystem`.

Members by declaring type:

    20  Ecng.IO.FastCsvReader
    10  Ecng.IO.IFileSystem
    9   Ecng.IO.FileSystemExtensions
    5   Ecng.IO.IOHelper
    5   Ecng.IO.LocalFileSystem
    5   Ecng.IO.MemoryFileSystem
    4   Ecng.IO.CsvFileWriter
    3   Ecng.IO.CsvFileReader
    1   Ecng.IO.CsvFileCommon
    1   Ecng.IO.TransactionFileStream

    types with no member reference
        Ecng.IO.EmptyLineBehavior

Two separable halves:
the CSV reader and writer at 28 members,
and the `IFileSystem` abstraction with its local and in-memory implementations at 30.
`Algo` alone accounts for 43 of the 63 references,
almost all of them in the storage layer.

### Ecng.ComponentModel

Version 1.0.454, pinned directly.
Ships `Ecng.ComponentModel.dll`; the fork binds 73 of its 151 public types and 152 of its 750 public members.
Size class: heavy.
Referencing projects: `Alerts.Interfaces`, `Algo`, `Algo.Compilation`, `Algo.Export`, `Algo.Import`,
`Algo.Indicators`, `Algo.Strategies`, `Algo.Testing`, `BusinessEntities`, `Charting.Interfaces`,
`Configuration`, `Diagram.Core`, `Localization`, `Messages`, `Reporting`, `Tests`.
Ecng-internal dependencies: `Ecng.Collections`, `Ecng.Common`, `Ecng.Localization`, `Ecng.Reflection`, `Ecng.Security`, `Ecng.Serialization`.
Ecng-internal dependents: `Ecng.Compilation.Python`, `Ecng.Data`, `Ecng.Logging`, `Ecng.Net`.
Public exposure: 22 types, including `Range<T>`, `WorkingTime`, `WorkingTimePeriod`,
`ServerCredentials`, `NotifiableObject`, `IRange<T>`, `ConnectionStates`, and `IconAttribute`.

Members by declaring type, showing every type above one member:

    23  Ecng.ComponentModel.Extensions
    10  Ecng.ComponentModel.WorkingTime
    9   Ecng.ComponentModel.ServerCredentials
    8   Ecng.ComponentModel.Range`1
    7   Ecng.ComponentModel.WorkingTimeExtensions
    6   Ecng.ComponentModel.IDebugger
    6   Ecng.ComponentModel.IRange
    5   Ecng.ComponentModel.ChannelExecutor
    5   Ecng.ComponentModel.WorkingTimePeriod
    4   Ecng.ComponentModel.NotifiableObject
    3   Ecng.ComponentModel.EntityPropertyHelper
    3   Ecng.ComponentModel.IRange`1
    3   Ecng.ComponentModel.StepAttribute
    3   Ecng.ComponentModel.ViewModelBase
    2   Ecng.ComponentModel.EntityProperty
    2   Ecng.ComponentModel.IScheduledTask
    2   Ecng.ComponentModel.ItemsSourceAttribute
    2   Ecng.ComponentModel.ItemsSourceBase`1
    2   Ecng.ComponentModel.OperatorRegistry
    2   Ecng.ComponentModel.TemplateEditorAttribute
    2   Ecng.ComponentModel.TimeSpanRangeAttribute

The remaining 43 types contribute one member each,
and 24 of those are numeric-range validation attributes in one family —
`IntGreaterThanZeroAttribute`, `DecimalNotNegativeAttribute`, `TimeSpanNullOrMoreZeroAttribute`, and their siblings.
The family is uniform enough to replace in a single pass.
Subtract it and the real surface drops to 128 members over 40 types,
concentrated in `Range<T>`, `WorkingTime`, and the `Extensions` helper.

### Ecng.Collections

Version 1.0.298, transitive through `Ecng.Common`'s dependents.
Ships `Ecng.Collections.dll`; the fork binds 36 of its 43 public types and 205 of its 592 public members.
Size class: heavy.
Referencing projects: `Alerts.Interfaces`, `Algo`, `Algo.Compilation`, `Algo.Gpu`, `Algo.Import`,
`Algo.Indicators`, `Algo.Strategies`, `Algo.Testing`, `BusinessEntities`, `Charting.Interfaces`,
`Configuration`, `Diagram.Core`, `Localization`, `MatchingEngine`, `Messages`, `Reporting`, `Tests`,
and the `Strategies.HistoryBollingerBands` and `Testing.HistoryConsole` samples.
Ecng-internal dependencies: `Ecng.Common`.
Ecng-internal dependents: `Ecng.Compilation`, `Ecng.Compilation.FSharp`, `Ecng.Compilation.Python`,
`Ecng.ComponentModel`, `Ecng.Data`, `Ecng.Logging`, `Ecng.Net`, `Ecng.Reflection`, `Ecng.Security`,
`Ecng.Serialization`, `Ecng.StringSearch`.
Public exposure: 14 types, including `SynchronizedDictionary<K,V>`, `SynchronizedList<T>`,
`SynchronizedSet<T>`, `CachedSynchronizedDictionary<K,V>`, `CircularBufferEx<T>`,
`NumericCircularBufferEx<T>`, `PriorityQueue<K,V>`, and `BaseOrderedChannel<K,V,T>`.

Members by declaring type:

    35  Ecng.Collections.CollectionHelper
    24  Ecng.Collections.BaseCollection`2
    14  Ecng.Collections.CircularBuffer`1
    14  Ecng.Collections.SynchronizedDictionary`2
    10  Ecng.Collections.BitArrayReader
    10  Ecng.Collections.KeyedCollection`2
    10  Ecng.Collections.SynchronizedPairSet`2
    9   Ecng.Collections.BaseOrderedChannel`3
    8   Ecng.Collections.CachedSynchronizedDictionary`2
    7   Ecng.Collections.SynchronizedSet`1
    6   Ecng.Collections.BitArrayWriter
    6   Ecng.Collections.INotifyCollection`1
    6   Ecng.Collections.NumericCircularBufferEx`1
    6   Ecng.Collections.PairSet`2
    6   Ecng.Collections.SynchronizedList`1
    5   Ecng.Collections.CachedSynchronizedSet`1
    5   Ecng.Collections.PriorityQueue`2
    4   Ecng.Collections.SynchronizedQueue`1
    4   Ecng.Collections.SynchronizedStack`1
    3   Ecng.Collections.CachedSynchronizedPairSet`2
    3   Ecng.Collections.SynchronizedKeyedCollection`2
    2   Ecng.Collections.CachedSynchronizedList`1
    2   Ecng.Collections.CachedSynchronizedOrderedDictionary`2
    2   Ecng.Collections.SynchronizedCollection`2
    1   Ecng.Collections.BackwardComparer`1
    1   Ecng.Collections.CircularBufferEx`1
    1   Ecng.Collections.DynamicTuple
    1   Ecng.Collections.ICircularBuffer`1

`Algo` alone accounts for 612 of the reference sites.
The Foundation project already covers seven of these types and part of `CollectionHelper`;
see the closing section.

### Ecng.Common

Version 1.0.261, transitive through 23 of the other 27 packages.
Ships `Ecng.Common.dll`; the fork binds 53 of its 104 public types and 258 of its 1615 public members.
Size class: heavy.
Referencing projects: 20 of the 23 measured fork assemblies —
every one except `Algo.Analytics`, `Foundation`, and `Media.Names` —
plus four samples.
Ecng-internal dependencies: none.
Ecng-internal dependents: 23 of the other 27 packages.
Public exposure: 14 types, including `Equatable<T>`, `Cloneable<T>`, `ICloneable<T>`, `IOperable<T>`,
`IdGenerator`, `IncrementalIdGenerator`, `Disposable`, `AsyncDisposable`, `CurrencyTypes`,
`CountryCodes`, `Platforms`, and `ComparisonOperator`.

Members by declaring type, showing every type above two members:

    39  Ecng.Common.StringHelper
    33  Ecng.Common.TimeHelper
    32  Ecng.Common.MathHelper
    19  Ecng.Common.AsyncHelper
    17  Ecng.Common.TypeHelper
    16  Ecng.Common.RandomGen
    8   Ecng.Common.Enumerator
    7   Ecng.Common.Equatable`1
    7   Ecng.Common.NullableHelper
    6   Ecng.Common.RefPair`2
    5   Ecng.Common.Do
    5   Ecng.Common.Scope`1
    4   Ecng.Common.AttributeHelper
    4   Ecng.Common.Converter
    4   Ecng.Common.IOperable`1
    3   Ecng.Common.Currency
    3   Ecng.Common.Disposable
    3   Ecng.Common.RefTuple
    3   Ecng.Common.ResettableLazy`1
    3   Ecng.Common.TupleHelper

The remaining 25 types contribute one or two members each, 37 members between them.
Six static helper classes hold 156 of the 258 members,
which makes them the natural unit of work:
`StringHelper`, `TimeHelper`, and `MathHelper` are pure functions over framework types
and carry no Ecng dependency of their own.

## Ecng-internal dependencies

Each Ecng assembly's own `AssemblyRef` table gives the graph.
Leaves-first applies inside this graph as well as at its edge,
because a pin falls only when no retained package still needs it.

    Ecng.Collections            Common
    Ecng.Common                 (none)
    Ecng.Compilation            Collections, Common, IO, Security, Serialization
    Ecng.Compilation.FSharp     Collections, Common, Compilation, Security
    Ecng.Compilation.Python     Collections, Common, Compilation, ComponentModel, Reflection
    Ecng.Compilation.Roslyn     Common, Compilation, Localization
    Ecng.ComponentModel         Collections, Common, Localization, Reflection, Security, Serialization
    Ecng.Configuration          Common
    Ecng.Data                   Collections, Common, ComponentModel, Serialization
    Ecng.Data.Ado               Common, Data
    Ecng.Data.SqlServer         Common, Data
    Ecng.Drawing                Common, Localization
    Ecng.Excel                  Common
    Ecng.Excel.OpenXml          Common, Excel
    Ecng.GeneticSharp           (none)
    Ecng.Interop                Common
    Ecng.IO                     Common
    Ecng.IO.Compression         Common, IO
    Ecng.Linq                   Common
    Ecng.Localization           (none)
    Ecng.Logging                Collections, Common, ComponentModel, IO, Localization, Serialization
    Ecng.Net                    Collections, Common, ComponentModel, Localization, Serialization
    Ecng.Reflection             Collections, Common
    Ecng.Security               Collections, Common, IO
    Ecng.Serialization          Collections, Common, IO, Reflection, Security
    Ecng.StringSearch           Collections
    Ecng.UnitTesting            Common, Serialization

Counted the other way, the packages that hold others in place:

    Ecng.Common            23 dependents
    Ecng.Collections       11
    Ecng.Serialization      6
    Ecng.IO                 5
    Ecng.Localization       5
    Ecng.ComponentModel     4
    Ecng.Security           4
    Ecng.Compilation        3
    Ecng.Reflection         3
    Ecng.Data               2
    Ecng.Excel              1

The other 16 packages have zero Ecng dependents,
which makes them the replaceable leaves.

## Replacement order

Three criteria order the work, applied in this sequence:

1. **Zero Ecng dependents.** The pin drops when the fork's consumer count reaches zero,
   and a package another Ecng package needs stays in the mirror regardless.
2. **Zero public exposure.** A package whose types never reach the fork's public surface
   replaces inside 1.x under decision record `0006`.
3. **Smallest consumed member count.** Among equals, the shortest contract goes first.

### Wave 1 — leaves with no public exposure

| rank | package | members | class | consumers |
| --- | --- | --- | --- | --- |
| 1 | Ecng.Compilation.All | 0 | trivial | done, see `Completed passes` |
| 2 | Ecng.Interop | 1 | trivial | one sample |
| 3 | Ecng.Net | 1 | trivial | `Algo` |
| 4 | Ecng.Linq | 2 | trivial | 4 projects |
| 5 | Ecng.IO.Compression | 2 | trivial | `Algo` |
| 6 | Ecng.Configuration | 4 | trivial | 10 projects |
| 7 | Ecng.StringSearch | 6 | trivial | `Algo` |

Ranks 2 through 5 are single-function ports with contract tests measured in tens of lines.
Rank 6 carries a wide migration behind a four-member contract,
and rank 7 replaces a Patricia trie whose behavior `Algo/SecurityTrie.cs` pins exactly.

### Wave 2 — leaves whose replacement implements a retained Ecng contract

| rank | package | members | class | pairs with |
| --- | --- | --- | --- | --- |
| 8 | Ecng.Data.Ado | 1 | trivial | Ecng.Data |
| 9 | Ecng.Data.SqlServer | 1 | trivial | Ecng.Data |
| 10 | Ecng.Compilation.FSharp | 1 | trivial | Ecng.Compilation |
| 11 | Ecng.Compilation.Python | 2 | trivial | Ecng.Compilation |
| 12 | Ecng.Compilation.Roslyn | 2 | trivial | Ecng.Compilation |
| 13 | Ecng.Excel.OpenXml | 2 | trivial | Ecng.Excel |

Each of these replaces a plug-in that implements an interface still owned by Ecng.
The pin drops all the same,
and the replacement keeps binding the package named beside it until that one falls too,
so scheduling the pair together keeps the intermediate state honest.

### Wave 3 — leaves that reach the public contract

| rank | package | members | public types | class |
| --- | --- | --- | --- | --- |
| 14 | Ecng.Drawing | 1 | 5 | trivial |
| 15 | Ecng.GeneticSharp | 30 | 3 | moderate |
| 16 | Ecng.UnitTesting | 48 | 1 (test-only) | moderate |
| 17 | Ecng.Logging | 59 | 6 | moderate |

Rank 16 is the cheapest of the four in practice,
because `StockSharp.Tests` ships to nobody
and its one public type crosses no consumer's compile.
Ranks 14, 15, and 17 each need a decision:
forward the types through a shim inside 1.x,
or queue the break as a `for 2.0` record.

### Wave 4 — internal nodes, as their dependents fall

| rank | package | members | class | unblocked by |
| --- | --- | --- | --- | --- |
| 18 | Ecng.Data | 10 | trivial | ranks 8, 9 |
| 19 | Ecng.Excel | 19 | trivial | rank 13 |
| 20 | Ecng.Compilation | 31 | moderate | ranks 10, 11, 12 |
| 21 | Ecng.ComponentModel | 152 | heavy | ranks 3, 11, 17, 18 |
| 22 | Ecng.Localization | 1 | trivial | ranks 3, 12, 14, 17, 21 |
| 23 | Ecng.Serialization | 53 | moderate | ranks 16, 18, 20, 21 |
| 24 | Ecng.Reflection | 10 | trivial | ranks 11, 21, 23 |
| 25 | Ecng.Security | 3 | trivial | ranks 10, 20, 21, 23 |
| 26 | Ecng.IO | 63 | moderate | ranks 5, 17, 20, 23, 25 |
| 27 | Ecng.Collections | 205 | heavy | ranks 7, 17, 20, 21, 23, 24, 25 |
| 28 | Ecng.Common | 258 | heavy | everything above |

The graph forces one uncomfortable ordering:
`Ecng.ComponentModel` at 152 members blocks `Ecng.Localization` at 1,
`Ecng.Reflection` at 10, and `Ecng.Security` at 3.
Those three small packages carry their pins until the heavy one is done.
Taking the four together as a single pass is the alternative,
and it is the shape the work will probably want by then.

### Reading the ranking

Waves 1 and 2 clear 13 of the 28 packages for 25 members of implementation,
and the pin list in `Directory.Build.targets` falls to 8 —
`Ecng.Compilation`, `Ecng.ComponentModel`, `Ecng.Data`, `Ecng.Drawing`,
`Ecng.Excel`, `Ecng.GeneticSharp`, `Ecng.Logging`, and `Ecng.UnitTesting`.
That is the whole leaves-first argument in one line.
Wave 3 costs 138 members and one contract decision.
Wave 4 holds 805 of the 968 measured members, 83 percent of the total,
and `Ecng.Common`, `Ecng.Collections`, and `Ecng.ComponentModel` alone are 615 of them.

## Completed passes

### Wave 1 rank 1 — Ecng.Compilation.All, dropped 2026-08-02

The meta-package declared four dependencies and shipped zero public types,
and no fork assembly held a reference to it:
`Algo.Compilation` already imported `Ecng.Compilation.FSharp`, `.Python`, and `.Roslyn`
and constructed `CSharpCompiler`, `VisualBasicCompiler`, `FSharpCompiler`, and `PythonCompiler` by name.
Naming those three packages in the csproj instead of the meta-package
left every binding in the tree unchanged
and removed the pin.

The pass wrote no code and needed no contract tests,
because a package with zero consumed members has no behavior to hold.
It cost two new pins:
`Ecng.Compilation.FSharp` and `Ecng.Compilation.Python` arrived transitively before
and are direct references now,
so the pin count moved from 19 to 20
while the closure fell from 28 packages to 27.

## What the Foundation collections already replaced

`Foundation/` holds the fork's first commissioned clean-room replacement,
built in three commits on 2026-08-01 and following the pattern decision record `0003` names.
It ships `StockSharp.Foundation.Collections`
with `SynchronizedDictionary<K,V>`, `SynchronizedList<T>`, `SynchronizedSet<T>`, `SynchronizedQueue<T>`,
their `Cached*` subclasses, the `ISynchronized` marker,
and the `CollectionExtensions` and `SynchronizedExtensions` helper sets —
seven files, about 1,000 lines, and 30 contract tests across
`Tests/FoundationCollectionContractTests.cs` and `Tests/FoundationExtensionContractTests.cs`,
each written from observed behavior rather than from the replaced source.
Measured against this inventory,
the shipped shape covers seven `Ecng.Collections` types worth 46 of that package's 205 consumed members,
plus 17 of `CollectionHelper`'s 35 —
63 members, or 31 percent of `Ecng.Collections`, already written.
Two projects run on it today, `Alerts.Interfaces` and `BusinessEntities`,
the other 17 consumers still bind Ecng's versions,
and `Ecng.Collections` keeps its full 205-member reading in the table above.
The two migrated projects each keep one residual reference,
and it names the coupling the rest of the program has to solve:
`Ecng.Serialization.SettingsStorage` derives from `Ecng.Collections.SynchronizedDictionary<string, object>`,
so `storage.ContainsKey(...)` at `Alerts.Interfaces/AlertRuleField.cs:72`
and `BusinessEntities/Candles/CandleSeries.cs:259` binds `Ecng.Collections` through a base class
no amount of local migration removes.
The gap between what Foundation implements and what the fork still imports
gives the clearest available estimate of a heavy package's real cost:
seven types and 63 members took three commits,
and `Ecng.Collections` carries 36 referenced types, 28 of them with member references, and 205 members.
