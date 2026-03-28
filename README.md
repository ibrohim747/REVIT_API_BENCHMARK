# Revit API Benchmark

A Revit external command that measures the performance of common data-access and computation patterns encountered in Revit plugin development. Results are written to a cumulative CSV file for comparison across runs.

## Requirements

- Autodesk Revit 2022+
- .NET Framework 4.8
- NuGet: `System.Memory`

## Installation

1. Build the project in Visual Studio (target: `AnyCPU`, framework: `net48`)
2. Copy the output DLL to your Revit addins folder
3. Create a `.addin` manifest pointing to `RevitBenchmark.BenchmarkCommand`

## Usage

Run the command from the Revit ribbon, select any wall element when prompted. The command executes all 12 benchmark sections and displays results in a `TaskDialog`. A CSV row is appended to:
```
%USERPROFILE%\Documents\RevitBenchmarks\BenchmarkResults.csv
```

The file is created with a header on first run; subsequent runs append rows, making it suitable for tracking performance across Revit versions or machines.

## Benchmark Sections

| # | Section | Iterations |
|---|---------|-----------|
| 1 | Memory footprint — Revit proxy, class (double/float), three struct layouts | — |
| 2 | Coordinate transform + `Math.Sqrt` | 1 000 000 |
| 3 | Area calculation `S = L × H − W × BaseOffset` | 10 000 000 |
| 4 | Boxing / unboxing per struct size | 1 000 000 |
| 5 | Array traversal — `T[]` vs `Span<T>` vs `Memory<T>` | 500 000 elements |
| 6 | `List<T>` fill and traversal — class vs struct vs `ValueTuple` | 1 000 000 elements |
| 7 | A\* single step — class (double/float) vs struct nodes | 10 000 reps |
| 8 | Parameter access — `get_Parameter(BIP)` vs `LookupParameter` vs cached | 10 000 |
| 9 | `FilteredElementCollector` — LINQ vs `ElementParameterFilter` vs `GetElement(id)` | 1 call each |
| 10 | Point transform — `Revit Transform.OfPoint` vs `Matrix4x4Float` | 1 000 000 |
| 11 | Buffer allocation — `new T[]` vs `ArrayPool<T>` vs `stackalloc` | 500 |
| 12 | Type check — `is` / `as` / pattern / `GetType()` / `Category.Id` | 5 000 000 |

## Data Representations

Each compute benchmark runs against five wall data representations to isolate the cost of the data layout itself:

| Name | Type | Fields | Notes |
|------|------|--------|-------|
| `WallDataClass` | `class` | `double` | Baseline managed heap object |
| `WallDataClassFloat` | `class` | `float` | Saves ~44 bytes on fields, heap scatter remains |
| `WallDataStruct` | `struct` | `float` | Value type, stack/inline allocation |
| `WallGeomStruct` | `struct` | `float` | Geometry fields only |
| `WallMetaStruct` | `struct` | `int` + `float` | Metadata fields only |

All structs use `[StructLayout(LayoutKind.Sequential)]`.

## Measurement Methodology

Every timed block follows this sequence:
```csharp
for (int i = 0; i < warmupRuns; i++) body();  // JIT warm-up
GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();                                  // clean GC state
var sw = Stopwatch.StartNew();
body();
sw.Stop();
return sw.Elapsed.TotalMilliseconds;           // double, not long
```

`TotalMilliseconds` (double) is used instead of `ElapsedMilliseconds` (long) to avoid truncation on sub-millisecond operations. ns/op is derived as `ms × 1 000 000 / iterations`.

## CSV Output

The output file contains one row per run. Column groups match the 12 benchmark sections. All timing values are in milliseconds (4 decimal places); memory sizes in bytes.
```
RunDateTime, WallId, WallTypeName,
Sz_*, FuncA_*, FuncB_*, Box_*, Span_*, List_*, AStar_*,
Param_*, Coll_*, Xform_*, Pool_*, Cast_*
```

The file is opened in append mode — it is never overwritten.

## Project Structure
```
RevitBenchmark/
├── Program.cs      # IExternalCommand entry point, iteration constants, TimeMs helper
├── Functions.cs    # BenchmarkCommand partial — all 12 Measure* methods + CSV writer
├── Classes.cs      # Data representations: WallDataClass, WallDataStruct, Matrix4x4Float, AStarNode, …
└── Csv.cs          # BenchmarkRow — field definitions, CsvHeader(), ToCsvLine()
```