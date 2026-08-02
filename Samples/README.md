# StockSharp API Examples Directory

This directory contains a collection of example projects and scripts demonstrating the use of the [StockSharp API](https://doc.stocksharp.com/en/topics/api.html) for developing trading applications.
These examples are intended specifically for developers who are programming in C# and are looking to create comprehensive trading robots or tools that do not integrate directly with the Designer platform.

## Overview

The examples provided in this folder serve as practical guides to various aspects of the [StockSharp API](https://doc.stocksharp.com/en/topics/api.html),
showcasing how to leverage its capabilities to access market data, execute trades, and manage portfolios.
These examples are particularly useful for users with a background in software development and an interest in financial markets.

## Contents

- **Basic API Usage**: Simple scripts demonstrating the initialization of the API, [connection to trading services](https://doc.stocksharp.com/en/topics/api/connectors.html), and basic data handling.
- **Advanced Trading Algorithms**: More complex examples that implement full [trading strategies](https://doc.stocksharp.com/en/topics/api/strategies.html) or algorithms using the StockSharp API.
- **Data Management**: Scripts that show how to fetch, store, and manage historical [market data](https://doc.stocksharp.com/en/topics/api/market_data_storage.html) for analysis.
- **Utility Tools**: Utility programs that assist with account management, [order placement](https://doc.stocksharp.com/en/topics/api/orders_management.html), and other trading operations.

## Purpose

The primary goal of this directory is to provide:
- A learning resource for new programmers who are just starting with trading software development.
- A reference point for experienced developers looking to explore different features of the StockSharp API.
- Code bases that can be extended or customized for personal trading solutions.

## Usage

Each example comes with a detailed explanation of the code and instructions on how to set it up and run it.
Developers are encouraged to modify and extend these examples to fit their specific trading needs and strategies.

## Important Note

Please note that the examples provided in this folder are not compatible with the Designer platform.
They are intended for direct use and modification within C# projects and require a solid understanding of programming concepts and the .NET.
Additionally, some examples may require adding a connector from StockSharp's private NuGet server.
For more information on accessing and using this server,
please visit [StockSharp's NuGet server manual](https://doc.stocksharp.com/en/topics/api/setup.html#private-nuget-server).

By exploring and utilizing these examples,
developers can gain a deeper understanding of how to implement robust trading solutions using the StockSharp API,
paving the way for the development of sophisticated trading robots and analytical tools.

## Building against this fork

`StockSharp_Samples.slnx` at the repository root
collects every sample this fork compiles from its own projects:

    dotnet build StockSharp_Samples.slnx -c Release

Each sample in that solution reaches the fork's libraries by `ProjectReference`.
`common_samples.props` supplies the reference to `Algo`,
and a sample needing more adds its own,
so an API change that breaks a sample breaks the build that ships it.
Decision record `docs/decisions/0005-rewire-samples-to-project-references.md` carries the reasoning.
`StockSharp.Samples.HistoryData` stays a `PackageReference`,
because it carries pinned market data rather than a library.

`.github/workflows/samples.yml` builds the same solution on ubuntu.
One operating system compile-checks documentation,
and every sample the solution holds targets `net10.0` with no platform-specific code.

## Excluded samples

Thirty-one of the thirty-five samples stay outside the solution.
A sample stays out when it references a `StockSharp.*` package
this repository holds no project for.
Three groups account for every exclusion:
the WPF control libraries `StockSharp.Xaml` and `StockSharp.Xaml.Charting`,
the connectors `common_connectors.props` adds
(`StockSharp.Binance`, `StockSharp.Okex`, `StockSharp.GateIO`, `StockSharp.Bitmex`, and `StockSharp.Fix`),
and the hosted products `StockSharp.Web.Api.Client` and `StockSharp.Studio.WebApi.UI`.
Each of those ships as an upstream binary,
so a sample bound to one compiles against the upstream this fork replaced,
and this repository holds no source to repair it from.

| Sample | Dependency this repository lacks |
| --- | --- |
| `01_Basic/01_ConnectAndDownloadInstruments` | `StockSharp.Xaml`, `common_connectors.props` |
| `01_Basic/02_MarketDepths` | `StockSharp.Xaml`, `common_connectors.props` |
| `01_Basic/03_Orders` | `StockSharp.Xaml`, `common_connectors.props` |
| `02_Candles/01_Realtime` | `StockSharp.Xaml.Charting`, `common_connectors.props` |
| `02_Candles/02_CombineHistoryRealtime` | `StockSharp.Xaml.Charting`, `common_connectors.props` |
| `03_Storage/03_RemoteSource` | `StockSharp.Web.Api.Client`, `StockSharp.Finam` |
| `03_Storage/04_HydraServerConnect` | `StockSharp.Xaml.Charting`, `StockSharp.Fix` |
| `03_Storage/05_HydraServerSaveToLocal` | `StockSharp.Fix` |
| `04_Indicators/01_SimpleSMA` | `StockSharp.Xaml.Charting` |
| `04_Indicators/02_ComplexBollinger` | `StockSharp.Xaml.Charting` |
| `04_Indicators/03_CreateOwn` | `StockSharp.Xaml.Charting` |
| `05_Chart/01_Chart` | `StockSharp.Xaml.Charting` |
| `05_Chart/02_ActiveOrders` | `StockSharp.Xaml.Charting` |
| `05_Chart/03_Performance` | `StockSharp.Xaml.Charting` |
| `06_Strategies/01_HistorySMA` | `StockSharp.Xaml.Charting` |
| `06_Strategies/02_HistoryBollingerBands` | `StockSharp.Xaml.Charting` |
| `06_Strategies/03_HistoryTrend` | `StockSharp.Xaml.Charting` |
| `06_Strategies/04_HistoryMarketRule` | `StockSharp.Xaml` |
| `06_Strategies/05_HistoryIndex` | `StockSharp.Xaml.Charting` |
| `06_Strategies/06_HistoryQuoting` | `StockSharp.Xaml.Charting` |
| `06_Strategies/07_LiveSpread` | `StockSharp.Xaml`, `common_connectors.props` |
| `06_Strategies/08_LiveArbitrage` | `StockSharp.Xaml`, `common_connectors.props` |
| `06_Strategies/09_LiveOptionsQuoting` | `StockSharp.Xaml.Charting`, `common_connectors.props` |
| `06_Strategies/10_LiveTerminal` | `StockSharp.Xaml`, `common_connectors.props` |
| `07_Testing/01_History` | `StockSharp.Xaml.Charting` |
| `07_Testing/02_Optimization` | `StockSharp.Xaml.Charting` |
| `07_Testing/03_RealTime` | `StockSharp.Xaml.Charting`, `common_connectors.props` |
| `08_Misc/01_Logging` | `StockSharp.Xaml` |
| `09_Advanced/01_MultiConnect` | `StockSharp.Xaml.Charting`, `StockSharp.Studio.WebApi.UI`, `common_connectors.props` |
| `09_Advanced/02_StoreDataLocal` | `StockSharp.Xaml.Charting`, `StockSharp.Studio.WebApi.UI`, `common_connectors.props` |
| `10_CrossPlatform/01_ConsoleApp` | `common_connectors.props`, for the `StockSharp.Binance` adapter it constructs |

The WPF samples restore and compile on Linux under `EnableWindowsTargeting`,
because the upstream control libraries publish to nuget.org.
That build binds the upstream binaries instead of the fork's projects,
which is the coupling decision record 0005 removes,
so those samples stay out.
The connector samples show what that coupling costs:
`03_Storage/03_RemoteSource` and `10_CrossPlatform/01_ConsoleApp` already fail to compile,
because the adapters those packages ship resolve their types
against an upstream `StockSharp.Messages` the fork has since changed.

Every `*_fromsrc.csproj` stays out as well.
Those project files reach sibling repositories
through `$(RepoAppsPath)`, `$(RepoGitHubPath)`, `$(ConnectorsGitHubPath)`,
and a hard-coded `..\..\..\..\StockSharpApps\` path,
none of which this fork checks out,
and each one duplicates the sample its plain `.csproj` neighbor already carries.