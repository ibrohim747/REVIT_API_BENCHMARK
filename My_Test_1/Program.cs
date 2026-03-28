using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Diagnostics;
using System.Text;

namespace RevitBenchmark
{
    [Transaction(TransactionMode.Manual)]
    public partial class BenchmarkCommand : IExternalCommand
    {
        private const int ITER = 1_000_000;
        private const int ITER_B = 10_000_000;
        private const int ITER_PARAM = 10_000;
        private const int ITER_CAST = 5_000_000;
        private const int ITER_XFORM = 1_000_000;
        private const int ITER_POOL = 500;
        private const int POOL_SIZE = 4096;
        private const int LIST_N = 1_000_000;
        private const int SPAN_N = 500_000;
        private const int ASTAR_REPS = 10_000;
        private const int ASTAR_PROJ = 200;

        private readonly StringBuilder _log = new StringBuilder();
        private BenchmarkRow _row;

        private static readonly string _headerLine = new string('═', 56);

        // JIT warm-up + GC flush before measurement; returns TotalMilliseconds (double)
        private static double TimeMs(Action body, int warmupRuns = 1)
        {
            for (int i = 0; i < warmupRuns; i++) body();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var sw = Stopwatch.StartNew();
            body();
            sw.Stop();
            return sw.Elapsed.TotalMilliseconds;
        }

        private static double NsPerOp(double ms, int iter) => ms * 1_000_000.0 / iter;

        public Result Execute(ExternalCommandData cData, ref string msg, ElementSet elems)
        {
            var uiDoc = cData.Application.ActiveUIDocument;
            var doc = uiDoc.Document;

            Reference refElem;
            try
            {
                refElem = uiDoc.Selection.PickObject(
                    Autodesk.Revit.UI.Selection.ObjectType.Element, "Select a wall");
            }
            catch { return Result.Cancelled; }

            var wall = doc.GetElement(refElem) as Wall;
            if (wall == null) { msg = "A wall must be selected!"; return Result.Failed; }

            // Read Revit parameters once
            var crv = (wall.Location as LocationCurve)?.Curve;
            var s0 = crv?.GetEndPoint(0) ?? XYZ.Zero;
            var e0 = crv?.GetEndPoint(1) ?? XYZ.Zero;
            double length = wall.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH)?.AsDouble() ?? 0;
            double height = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM)?.AsDouble() ?? 0;
            double width = wall.WallType.Width;
            double baseOff = wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET)?.AsDouble() ?? 0;
            double topOff = wall.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET)?.AsDouble() ?? 0;
            int typeId = wall.WallType.Id.IntegerValue;
            int lvlId = wall.LevelId.IntegerValue;
            int wallId = wall.Id.IntegerValue;
            string tName = wall.WallType.Name;

            var e2 = new WallDataClass
            {
                Id = wallId,
                TypeId = typeId,
                LevelId = lvlId,
                TypeName = tName,
                StartX = s0.X,
                StartY = s0.Y,
                StartZ = s0.Z,
                EndX = e0.X,
                EndY = e0.Y,
                EndZ = e0.Z,
                Length = length,
                Height = height,
                Width = width,
                BaseOffset = baseOff,
                TopOffset = topOff
            };
            var e2f = new WallDataClassFloat
            {
                Id = wallId,
                TypeId = typeId,
                LevelId = lvlId,
                TypeName = tName,
                StartX = (float)s0.X,
                StartY = (float)s0.Y,
                StartZ = (float)s0.Z,
                EndX = (float)e0.X,
                EndY = (float)e0.Y,
                EndZ = (float)e0.Z,
                Length = (float)length,
                Height = (float)height,
                Width = (float)width,
                BaseOffset = (float)baseOff,
                TopOffset = (float)topOff
            };
            var e3 = new WallDataStruct
            {
                Id = wallId,
                TypeId = typeId,
                LevelId = lvlId,
                StartX = (float)s0.X,
                StartY = (float)s0.Y,
                StartZ = (float)s0.Z,
                EndX = (float)e0.X,
                EndY = (float)e0.Y,
                EndZ = (float)e0.Z,
                Length = (float)length,
                Height = (float)height,
                Width = (float)width,
                BaseOffset = (float)baseOff,
                TopOffset = (float)topOff
            };
            var e41 = new WallGeomStruct
            {
                StartX = (float)s0.X,
                StartY = (float)s0.Y,
                StartZ = (float)s0.Z,
                EndX = (float)e0.X,
                EndY = (float)e0.Y,
                EndZ = (float)e0.Z,
                Length = (float)length,
                Height = (float)height,
                Width = (float)width
            };
            var e42 = new WallMetaStruct
            {
                Id = wallId,
                TypeId = typeId,
                LevelId = lvlId,
                BaseOffset = (float)baseOff,
                TopOffset = (float)topOff
            };

            _row = new BenchmarkRow
            {
                RunDateTime = DateTime.Now,
                WallId = wallId,
                WallTypeName = tName
            };

            _log.Clear();
            Header("REVIT API BENCHMARK — Results");

            Section("1. MEMORY SIZES");
            MeasureSizes(e2, e2f, e3, e41, e42);

            Section("2. FUNCTION A — Coordinate Transform (√)");
            MeasureFuncA(doc, wall, e2, e2f, e3, e41, e42);

            Section("3. FUNCTION B — Area Calculation (×)");
            MeasureFuncB(e2, e2f, e3, e41, e42);

            Section("4. BOXING / UNBOXING");
            MeasureBoxing(e3, e41, e42);

            Section($"5. Span<T> and Memory<T>  ({SPAN_N} elements)");
            MeasureSpanMemory(e3, e41, e42);

            Section($"6. List<T>  ({LIST_N} elements)");
            MeasureLists(e2, e2f, e3, e41, e42);

            Section($"7. A* — 1 step × {ASTAR_REPS} reps, projection {ASTAR_PROJ}");
            MeasureAStar(e2, e2f, e3, e41);

            Section("8. PARAMETER ACCESS — BIP vs LookupParameter vs cached");
            MeasureParameterAccess(doc, wall);

            Section("9. LINQ vs FAST FILTERS — FilteredElementCollector");
            MeasureCollectorFilters(doc, wall);

            Section("10. TRANSFORM — Revit Transform vs Matrix4x4Float");
            MeasureTransform(wall);

            Section("11. ALLOCATION — new T[] vs ArrayPool<T>");
            MeasureArrayPool();

            Section("12. TYPE CHECK — is vs as vs Category.Id");
            MeasureTypeCast(wall);

            string csvPath = SaveCsv();

            new TaskDialog("Benchmark Results")
            {
                MainInstruction = "Done!",
                MainContent = _log.ToString(),
                FooterText = "📊 " + csvPath
            }.Show();

            return Result.Succeeded;
        }
    }
}