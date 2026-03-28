using Autodesk.Revit.DB;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace RevitBenchmark
{
    public partial class BenchmarkCommand
    {
        // =============================================================
        //  1. Memory sizes
        // =============================================================
        private void MeasureSizes(
            WallDataClass e2, WallDataClassFloat e2f,
            WallDataStruct e3, WallGeomStruct e41, WallMetaStruct e42)
        {
            int s3 = Marshal.SizeOf<WallDataStruct>();
            int s41 = Marshal.SizeOf<WallGeomStruct>();
            int s42 = Marshal.SizeOf<WallMetaStruct>();

            _row.Sz_RevitProxy_bytes = 16 + 8 + 32;
            _row.Sz_ClassDouble_bytes = 16 + 11 * 8 + 2 * 4 + 8;
            _row.Sz_ClassFloat_bytes = 16 + 11 * 4 + 2 * 4 + 8;
            _row.Sz_Struct_bytes = s3;
            _row.Sz_GeomStruct_bytes = s41;
            _row.Sz_MetaStruct_bytes = s42;

            L($"  Elem 1   (Revit proxy, managed): ~{16 + 8 + 32} bytes  +  C++ core (inaccessible)");
            L($"  Elem 2   (WallDataClass  double): ~{16 + 11 * 8 + 2 * 4 + 8} bytes  +  string ~{26 + (e2.TypeName?.Length ?? 0) * 2} bytes");
            L($"  Elem 2b  (WallDataClassFloat):    ~{16 + 11 * 4 + 2 * 4 + 8} bytes  +  string ~{26 + (e2f.TypeName?.Length ?? 0) * 2} bytes  ← float");
            L($"  Elem 3   (WallDataStruct):  {s3} bytes  ← value type");
            L($"  Elem 4.1 (WallGeomStruct):  {s41} bytes  ← geometry");
            L($"  Elem 4.2 (WallMetaStruct):  {s42} bytes  ← metadata");
            L($"  └─ 4.1+4.2 = {s41 + s42} bytes  (diff vs Elem 3: {s3 - (s41 + s42)} bytes)");
            L($"  NOTE: WallDataClassFloat saves ~{11 * 8 - 11 * 4} bytes on fields, but heap scatter remains.");
        }

        // =============================================================
        //  2. Function A — coordinate transform + √
        // =============================================================
        private void MeasureFuncA(
            Document doc, Wall wall,
            WallDataClass e2, WallDataClassFloat e2f,
            WallDataStruct e3, WallGeomStruct e41, WallMetaStruct e42)
        {
            L($"  StartX+=0.001, EndX+=0.001, Length=√(...)  |  iterations: {ITER:N0}");

            var sw = Stopwatch.StartNew();
            using (var tx = new Transaction(doc, "Bench_Move"))
            {
                tx.Start();
                ElementTransformUtils.MoveElement(doc, wall.Id, new XYZ(0.1, 0, 0));
                ElementTransformUtils.MoveElement(doc, wall.Id, new XYZ(-0.1, 0, 0));
                tx.Commit();
            }
            sw.Stop();
            _row.FuncA_RevitAPI_ms = sw.Elapsed.TotalMilliseconds;
            L($"\n  Elem 1 (Revit API, 1 call): {sw.ElapsedMilliseconds,6} ms  ← Transaction+regen+UI\n");

            // .NET Framework 4.8: MathF unavailable → cast from Math.Sqrt
            float SqrtLen(float dx, float dy) => (float)Math.Sqrt(dx * dx + dy * dy);

            // Save initial values so every loop starts with identical data
            double e2_sx0 = e2.StartX, e2_ex0 = e2.EndX;
            float e2f_sx0 = e2f.StartX, e2f_ex0 = e2f.EndX;
            float e3_sx0 = e3.StartX, e3_ex0 = e3.EndX;
            float e41_sx0 = e41.StartX, e41_ex0 = e41.EndX;

            double t2 = TimeMs(() => {
                e2.StartX = e2_sx0; e2.EndX = e2_ex0;
                for (int i = 0; i < ITER; i++) { e2.StartX += 0.001; e2.EndX += 0.001; e2.Length = Math.Sqrt((e2.EndX - e2.StartX) * (e2.EndX - e2.StartX) + (e2.EndY - e2.StartY) * (e2.EndY - e2.StartY)); }
            });
            double t2f = TimeMs(() => {
                e2f.StartX = e2f_sx0; e2f.EndX = e2f_ex0;
                for (int i = 0; i < ITER; i++) { e2f.StartX += 0.001f; e2f.EndX += 0.001f; e2f.Length = SqrtLen(e2f.EndX - e2f.StartX, e2f.EndY - e2f.StartY); }
            });
            double t3 = TimeMs(() => {
                e3.StartX = e3_sx0; e3.EndX = e3_ex0;
                for (int i = 0; i < ITER; i++) { e3.StartX += 0.001f; e3.EndX += 0.001f; e3.Length = SqrtLen(e3.EndX - e3.StartX, e3.EndY - e3.StartY); }
            });
            double t41 = TimeMs(() => {
                e41.StartX = e41_sx0; e41.EndX = e41_ex0;
                for (int i = 0; i < ITER; i++) { e41.StartX += 0.001f; e41.EndX += 0.001f; e41.Length = SqrtLen(e41.EndX - e41.StartX, e41.EndY - e41.StartY); }
            });
            double t42 = TimeMs(() => { for (int i = 0; i < ITER; i++) { e42.BaseOffset += 0.001f; e42.TopOffset += 0.001f; } });

            _row.FuncA_ClassDouble_ms = t2;
            _row.FuncA_ClassFloat_ms = t2f;
            _row.FuncA_Struct_ms = t3;
            _row.FuncA_GeomStruct_ms = t41;
            _row.FuncA_MetaStruct_ms = t42;

            L($"  Elem 2   (class  double): {t2,6:F1} ms");
            L($"  Elem 2b  (class  float):  {t2f,6:F1} ms");
            L($"  Elem 3   (struct float):  {t3,6:F1} ms");
            L($"  Elem 4.1 (geometry):      {t41,6:F1} ms");
            L($"  Elem 4.2 (meta, no √):    {t42,6:F1} ms");
        }

        // =============================================================
        //  3. Function B — area calculation
        // =============================================================
        private void MeasureFuncB(
            WallDataClass e2, WallDataClassFloat e2f,
            WallDataStruct e3, WallGeomStruct e41, WallMetaStruct e42)
        {
            L($"  S = Length × Height - Width × BaseOffset  |  iterations: {ITER_B:N0}\n");

            double r2 = 0; float r2f = 0, r3 = 0, r41 = 0, r42 = 0;

            double t2 = TimeMs(() => { for (int i = 0; i < ITER_B; i++) r2 = e2.Length * e2.Height - e2.Width * e2.BaseOffset; });
            double t2f = TimeMs(() => { for (int i = 0; i < ITER_B; i++) r2f = e2f.Length * e2f.Height - e2f.Width * e2f.BaseOffset; });
            double t3 = TimeMs(() => { for (int i = 0; i < ITER_B; i++) r3 = e3.Length * e3.Height - e3.Width * e3.BaseOffset; });
            double t41 = TimeMs(() => { for (int i = 0; i < ITER_B; i++) r41 = e41.Length * e41.Height - e41.Width; });
            double t42 = TimeMs(() => { for (int i = 0; i < ITER_B; i++) r42 = e42.BaseOffset + e42.TopOffset; });

            GC.KeepAlive(r2); GC.KeepAlive(r2f); GC.KeepAlive(r3); GC.KeepAlive(r41); GC.KeepAlive(r42);

            _row.FuncB_ClassDouble_ms = t2;
            _row.FuncB_ClassFloat_ms = t2f;
            _row.FuncB_Struct_ms = t3;
            _row.FuncB_GeomStruct_ms = t41;
            _row.FuncB_MetaStruct_ms = t42;

            L($"  Elem 2   (class  double): {t2,6:F1} ms");
            L($"  Elem 2b  (class  float):  {t2f,6:F1} ms");
            L($"  Elem 3   (struct float):  {t3,6:F1} ms");
            L($"  Elem 4.1 (geometry):      {t41,6:F1} ms");
            L($"  Elem 4.2 (meta, add only):{t42,6:F1} ms");
        }

        // =============================================================
        //  4. Boxing / Unboxing
        // =============================================================
        private void MeasureBoxing(WallDataStruct e3, WallGeomStruct e41, WallMetaStruct e42)
        {
            L($"  value → object → value  |  iterations: {ITER:N0}");
            L($"  Elem 1/2/2b (class): boxing not applicable — reference type\n");

            double Boxing<T>(T v) where T : struct
            {
                object b = null;
                return TimeMs(() => { for (int i = 0; i < ITER; i++) { b = v; v = (T)b; } });
            }

            double b3 = Boxing(e3), b41 = Boxing(e41), b42 = Boxing(e42);

            _row.Box_Struct_ms = b3;
            _row.Box_GeomStruct_ms = b41;
            _row.Box_MetaStruct_ms = b42;

            L($"  Elem 3   ({Marshal.SizeOf<WallDataStruct>(),2} bytes): {b3,6:F1} ms");
            L($"  Elem 4.1 ({Marshal.SizeOf<WallGeomStruct>(),2} bytes): {b41,6:F1} ms");
            L($"  Elem 4.2 ({Marshal.SizeOf<WallMetaStruct>(),2} bytes): {b42,6:F1} ms");
            L($"  NOTE: larger struct = more expensive boxing. Prefer List<T> over List<object>.");
        }

        // =============================================================
        //  5. Span<T> and Memory<T>
        // =============================================================
        private void MeasureSpanMemory(WallDataStruct e3, WallGeomStruct e41, WallMetaStruct e42)
        {
            L($"  {SPAN_N:N0} elements  |  real fields read (JIT won't elide the loop)");
            L($"  Elem 2/2b (class): Span<T>/Memory<T> not applicable — reference type\n");
            L($"  {"Type",-18}  {"T[]",5}  {"Span",5}  {"Memory",6}  bytes/elem");

            // .NET Framework 4.8: Array.Fill unavailable → manual fill
            var arr3 = new WallDataStruct[SPAN_N]; for (int i = 0; i < SPAN_N; i++) arr3[i] = e3;
            var arr41 = new WallGeomStruct[SPAN_N]; for (int i = 0; i < SPAN_N; i++) arr41[i] = e41;
            var arr42 = new WallMetaStruct[SPAN_N]; for (int i = 0; i < SPAN_N; i++) arr42[i] = e42;

            // mode 0=T[], 1=Span, 2=Memory
            double Via<T>(T[] arr, Func<T, float> sel, int mode) where T : struct
                => TimeMs(() => {
                    float acc = 0;
                    if (mode == 0) { for (int i = 0; i < arr.Length; i++) acc += sel(arr[i]); }
                    else if (mode == 1) { var sp = new Span<T>(arr); for (int i = 0; i < sp.Length; i++) acc += sel(sp[i]); }
                    else { var sp = new Memory<T>(arr).Span; for (int i = 0; i < sp.Length; i++) acc += sel(sp[i]); }
                    GC.KeepAlive(acc);
                });

            double s3_a = Via(arr3, x => x.Length + x.Height, 0);
            double s3_sp = Via(arr3, x => x.Length + x.Height, 1);
            double s3_m = Via(arr3, x => x.Length + x.Height, 2);
            double g_a = Via(arr41, x => x.Length + x.Height, 0);
            double g_sp = Via(arr41, x => x.Length + x.Height, 1);
            double g_m = Via(arr41, x => x.Length + x.Height, 2);
            double mt_a = Via(arr42, x => x.BaseOffset + x.TopOffset, 0);
            double mt_sp = Via(arr42, x => x.BaseOffset + x.TopOffset, 1);
            double mt_m = Via(arr42, x => x.BaseOffset + x.TopOffset, 2);

            _row.Span_Struct_Array_ms = s3_a; _row.Span_Struct_Span_ms = s3_sp; _row.Span_Struct_Memory_ms = s3_m;
            _row.Span_Geom_Array_ms = g_a; _row.Span_Geom_Span_ms = g_sp; _row.Span_Geom_Memory_ms = g_m;
            _row.Span_Meta_Array_ms = mt_a; _row.Span_Meta_Span_ms = mt_sp; _row.Span_Meta_Memory_ms = mt_m;

            L($"  {"Elem3 (struct)",-18}  {s3_a,5:F1}  {s3_sp,5:F1}  {s3_m,6:F1}  {Marshal.SizeOf<WallDataStruct>(),6}");
            L($"  {"Elem4.1 (geom)",-18}  {g_a,5:F1}  {g_sp,5:F1}  {g_m,6:F1}  {Marshal.SizeOf<WallGeomStruct>(),6}");
            L($"  {"Elem4.2 (meta)",-18}  {mt_a,5:F1}  {mt_sp,5:F1}  {mt_m,6:F1}  {Marshal.SizeOf<WallMetaStruct>(),6}");
            L($"\n  T[]      — bounds check on every access");
            L($"  Span<T>  — JIT elides bounds checks, zero allocation");
            L($"  Memory<T>— for async/await; internally the same Span");
        }

        // =============================================================
        //  6. List<T>
        // =============================================================
        private void MeasureLists(
            WallDataClass e2, WallDataClassFloat e2f,
            WallDataStruct e3, WallGeomStruct e41, WallMetaStruct e42)
        {
            L($"  {"Type",-26}  {"Fill",7}  {"Trav",5}  {"Mem",8}");

            (double fill, double trav) Row<T>(string label, Func<T> factory, Func<T, float> sel, long memBytes)
            {
                double fill = TimeMs(() => { var lst = new List<T>(LIST_N); for (int i = 0; i < LIST_N; i++) lst.Add(factory()); GC.KeepAlive(lst); });
                var ready = new List<T>(LIST_N);
                for (int i = 0; i < LIST_N; i++) ready.Add(factory());
                double trav = TimeMs(() => { float acc = 0; foreach (var x in ready) acc += sel(x); GC.KeepAlive(acc); });
                L($"  {label,-26}  {fill,5:F1}ms  {trav,5:F1}ms  {memBytes / 1024,6}KB");
                return (fill, trav);
            }

            (_row.List_ClassDouble_Fill_ms, _row.List_ClassDouble_Trav_ms) =
                Row("List<WallDataClass>",
                    () => new WallDataClass { Id = e2.Id, Length = e2.Length, Height = e2.Height, Width = e2.Width, TypeName = e2.TypeName },
                    x => (float)(x.Length + x.Height), (8 + 120L) * LIST_N);

            (_row.List_ClassFloat_Fill_ms, _row.List_ClassFloat_Trav_ms) =
                Row("List<WallDataClassFloat>",
                    () => new WallDataClassFloat { Id = e2f.Id, Length = e2f.Length, Height = e2f.Height, Width = e2f.Width, TypeName = e2f.TypeName },
                    x => x.Length + x.Height, (8 + 76L) * LIST_N);

            (_row.List_Struct_Fill_ms, _row.List_Struct_Trav_ms) =
                Row("List<WallDataStruct>", () => e3, x => x.Length + x.Height, (long)Marshal.SizeOf<WallDataStruct>() * LIST_N);

            (_row.List_GeomStruct_Fill_ms, _row.List_GeomStruct_Trav_ms) =
                Row("List<WallGeomStruct>", () => e41, x => x.Length + x.Height, (long)Marshal.SizeOf<WallGeomStruct>() * LIST_N);

            (_row.List_MetaStruct_Fill_ms, _row.List_MetaStruct_Trav_ms) =
                Row("List<WallMetaStruct>", () => e42, x => x.BaseOffset + x.TopOffset, (long)Marshal.SizeOf<WallMetaStruct>() * LIST_N);

            var lstT = new List<(WallGeomStruct g, WallMetaStruct m)>(LIST_N);
            double fillT = TimeMs(() => { for (int i = 0; i < LIST_N; i++) lstT.Add((e41, e42)); });
            double travT = TimeMs(() => { float acc = 0; foreach (var (g, m) in lstT) acc += g.Length + m.BaseOffset; GC.KeepAlive(acc); });
            long memT = (long)(Marshal.SizeOf<WallGeomStruct>() + Marshal.SizeOf<WallMetaStruct>()) * LIST_N;

            _row.List_Tuple_Fill_ms = fillT;
            _row.List_Tuple_Trav_ms = travT;
            L($"  {"List<(Geom, Meta)>",-26}  {fillT,5:F1}ms  {travT,5:F1}ms  {memT / 1024,6}KB");
            L($"\n  List<class>  — heap scatter, cache miss on traversal");
            L($"  List<struct> — inline in backing array, CPU prefetcher happy");
        }

        // =============================================================
        //  7. A*
        // =============================================================
        private void MeasureAStar(
            WallDataClass e2, WallDataClassFloat e2f,
            WallDataStruct e3, WallGeomStruct e41)
        {
            const float STEP = 0.1f;
            L($"  Start → Goal along wall axis  |  8-connected grid  |  step {STEP}");
            L($"  Reps: {ASTAR_REPS:N0}  |  Projection: {ASTAR_PROJ} steps (~{ASTAR_PROJ * STEP:F1} units)\n");
            L($"  {"Type",-28}  {"ns/step",10}  {"×proj ms",10}  Node");

            // .NET Framework 4.8: MathF unavailable → cast from Math.Sqrt
            double AStarStruct(float sx, float sy, float gx, float gy)
            {
                var origin = new AStarNode { X = sx, Y = sy, G = 0, H = (float)Math.Sqrt((gx - sx) * (gx - sx) + (gy - sy) * (gy - sy)) };
                var sw = Stopwatch.StartNew();
                for (int r = 0; r < ASTAR_REPS; r++)
                {
                    var cur = origin; float bestF = float.MaxValue; AStarNode best = default;
                    for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        float nx = cur.X + dx * STEP, ny = cur.Y + dy * STEP;
                        float ng = cur.G + ((dx != 0 && dy != 0) ? 1.41421356f * STEP : STEP);
                        float dxg = gx - nx, dyg = gy - ny, nf = ng + (float)Math.Sqrt(dxg * dxg + dyg * dyg);
                        if (nf < bestF) { bestF = nf; best = new AStarNode { X = nx, Y = ny, G = ng, H = nf - ng, ParentIdx = r }; }
                    }
                    GC.KeepAlive(best);
                }
                return sw.Elapsed.TotalMilliseconds * 1_000_000.0 / ASTAR_REPS;
            }

            double AStarClass(double sx, double sy, double gx, double gy)
            {
                var origin = new AStarNodeClass { X = sx, Y = sy, G = 0, H = Math.Sqrt((gx - sx) * (gx - sx) + (gy - sy) * (gy - sy)) };
                var sw = Stopwatch.StartNew();
                for (int r = 0; r < ASTAR_REPS; r++)
                {
                    var cur = origin; double bestF = double.MaxValue; AStarNodeClass best = null;
                    for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        double nx = cur.X + dx * STEP, ny = cur.Y + dy * STEP;
                        double ng = cur.G + ((dx != 0 && dy != 0) ? 1.41421356 * STEP : STEP);
                        double dxg = gx - nx, dyg = gy - ny, nf = ng + Math.Sqrt(dxg * dxg + dyg * dyg);
                        if (nf < bestF) { bestF = nf; best = new AStarNodeClass { X = nx, Y = ny, G = ng, H = nf - ng, Parent = cur }; }
                    }
                    GC.KeepAlive(best);
                }
                return sw.Elapsed.TotalMilliseconds * 1_000_000.0 / ASTAR_REPS;
            }

            void Row(string label, double ns, string nodeInfo)
                => L($"  {label,-28}  {ns,8:F1} ns  {ns * ASTAR_PROJ / 1_000_000.0,8:F3} ms  {nodeInfo}");

            double ns2 = AStarClass(e2.StartX, e2.StartY, e2.EndX, e2.EndY);
            double ns2f = AStarClass(e2f.StartX, e2f.StartY, e2f.EndX, e2f.EndY);
            double ns3 = AStarStruct(e3.StartX, e3.StartY, e3.EndX, e3.EndY);
            double ns41 = AStarStruct(e41.StartX, e41.StartY, e41.EndX, e41.EndY);

            _row.AStar_ClassDouble_nsPerStep = ns2;
            _row.AStar_ClassFloat_nsPerStep = ns2f;
            _row.AStar_Struct_nsPerStep = ns3;
            _row.AStar_GeomStruct_nsPerStep = ns41;

            Row("Elem 2  (class double)", ns2, "~44 bytes (heap)");
            Row("Elem 2b (class float)", ns2f, "~44 bytes (float src)");
            Row("Elem 3  (struct float)", ns3, $"{Marshal.SizeOf<AStarNode>()} bytes (value)");
            Row("Elem 4.1 (geom struct)", ns41, $"{Marshal.SizeOf<AStarNode>()} bytes (value)");
            L($"  Elem 4.2 (meta): no coordinates — A* not applicable");
            L($"  Formula: ms = ns/step × step_count / 1_000_000");
        }

        // =============================================================
        //  8. Parameter access
        // =============================================================
        private void MeasureParameterAccess(Document doc, Wall wall)
        {
            L($"  Read WALL_USER_HEIGHT_PARAM  |  iterations: {ITER_PARAM:N0}\n");

            string pName = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM)?.Definition?.Name ?? "Unconnected Height";
            var cached = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM);
            double r = 0;

            double tBIP = TimeMs(() => { for (int i = 0; i < ITER_PARAM; i++) r = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble(); });
            double tLookup = TimeMs(() => { for (int i = 0; i < ITER_PARAM; i++) r = wall.LookupParameter(pName)?.AsDouble() ?? 0; });
            double tCached = TimeMs(() => { for (int i = 0; i < ITER_PARAM; i++) r = cached.AsDouble(); });
            GC.KeepAlive(r);

            _row.Param_BIP_ms = tBIP;
            _row.Param_Lookup_ms = tLookup;
            _row.Param_Cached_ms = tCached;

            L($"  {"Method",-34}  {"ms",8}  {"ns/call",10}");
            L($"  {new string('─', 57)}");
            L($"  {"get_Parameter(BuiltInParameter)",-34}  {tBIP,8:F2}  {NsPerOp(tBIP, ITER_PARAM),10:F1}");
            L($"  {"LookupParameter(\"name\")",-34}  {tLookup,8:F2}  {NsPerOp(tLookup, ITER_PARAM),10:F1}");
            L($"  {"cached: var p=...; p.AsDouble()",-34}  {tCached,8:F2}  {NsPerOp(tCached, ITER_PARAM),10:F1}");
            L($"  NOTE: LookupParameter is {(tBIP > 0 ? tLookup / tBIP : 0):F1}× slower than BIP (linear search).");
            L($"         Cache Parameter at init — avoid get_Parameter inside loops.");
        }

        // =============================================================
        //  9. Slow vs Fast Filters
        // =============================================================
        private void MeasureCollectorFilters(Document doc, Wall wall)
        {
            L($"  Find WallType by name (single call — real plugin scenario)\n");

            string typeName = wall.WallType.Name;
            WallType foundLinq = null, foundFast = null;

            double tLinq = TimeMs(() => foundLinq = new FilteredElementCollector(doc)
                .OfClass(typeof(WallType)).Cast<WallType>()
                .FirstOrDefault(wt => wt.Name == typeName));

            double tFast = TimeMs(() => {
                var rule = ParameterFilterRuleFactory.CreateEqualsRule(
                    new ElementId(BuiltInParameter.ALL_MODEL_TYPE_NAME), typeName, false);
                foundFast = new FilteredElementCollector(doc)
                    .OfClass(typeof(WallType)).WherePasses(new ElementParameterFilter(rule))
                    .FirstOrDefault() as WallType;
            });

            double tById = TimeMs(() => { var _ = doc.GetElement(wall.WallType.Id) as WallType; });

            _row.Coll_Linq_ms = tLinq;
            _row.Coll_FastFilter_ms = tFast;
            _row.Coll_ById_ms = tById;

            L($"  {"Method",-42}  {"ms",8}  Found");
            L($"  {new string('─', 60)}");
            L($"  {"LINQ .FirstOrDefault(wt => wt.Name == ...)",-42}  {tLinq,8:F2}  {foundLinq?.Name ?? "—"}");
            L($"  {"ElementParameterFilter (fast filter)",-42}  {tFast,8:F2}  {foundFast?.Name ?? "—"}");
            L($"  {"doc.GetElement(knownId)",-42}  {tById,8:F2}  direct O(1)");
            L($"  NOTE: Fast filter runs on the C++ side — significantly faster than LINQ.");
            L($"         If ElementId is already known, doc.GetElement(id) is fastest.");
        }

        // =============================================================
        //  10. Transform vs Matrix4x4Float
        // =============================================================
        private void MeasureTransform(Wall wall)
        {
            L($"  Point transform (45° rotation + translation)  |  iterations: {ITER_XFORM:N0}\n");

            var pt = (wall.Location as LocationCurve)?.Curve?.GetEndPoint(0) ?? XYZ.Zero;
            var ptF = new XYZFloat((float)pt.X, (float)pt.Y, (float)pt.Z);

            double angle = Math.PI / 4.0;
            var revitT = Transform.CreateRotation(XYZ.BasisZ, angle)
                                     .Multiply(Transform.CreateTranslation(new XYZ(1, 2, 0)));
            float ca = (float)Math.Cos(angle), sa = (float)Math.Sin(angle);
            var mat = new Matrix4x4Float
            {
                M00 = ca,
                M01 = -sa,
                M02 = 0,
                M03 = 1f,
                M10 = sa,
                M11 = ca,
                M12 = 0,
                M13 = 2f,
                M20 = 0,
                M21 = 0,
                M22 = 1,
                M23 = 0f,
                M30 = 0,
                M31 = 0,
                M32 = 0,
                M33 = 1f
            };

            XYZ accR = XYZ.Zero; XYZFloat accM = default;
            double tRevit = TimeMs(() => { for (int i = 0; i < ITER_XFORM; i++) accR = revitT.OfPoint(pt); });
            double tMat = TimeMs(() => { for (int i = 0; i < ITER_XFORM; i++) accM = mat.TransformPoint(ptF); });
            GC.KeepAlive(accR); GC.KeepAlive(accM);

            _row.Xform_Revit_ms = tRevit;
            _row.Xform_Matrix_ms = tMat;

            double nsR = NsPerOp(tRevit, ITER_XFORM), nsM = NsPerOp(tMat, ITER_XFORM);
            L($"  {"Method",-34}  {"ms",8}  {"ns/call",10}  Note");
            L($"  {new string('─', 68)}");
            L($"  {"Revit Transform.OfPoint",-34}  {tRevit,8:F2}  {nsR,10:F2}  managed heap, double");
            L($"  {"Matrix4x4Float.TransformPoint",-34}  {tMat,8:F2}  {nsM,10:F2}  value type, float");
            L($"  NOTE: Matrix4x4Float is {nsR / Math.Max(nsM, 0.001):F1}× faster — use for geometry export.");
        }

        // =============================================================
        //  11. ArrayPool vs new T[]
        // =============================================================
        private void MeasureArrayPool()
        {
            L($"  Buffer: {POOL_SIZE} elements  |  allocation iterations: {ITER_POOL:N0}\n");

            float sumNew = 0, sumPool = 0, sumStack = 0;

            double tNew = TimeMs(() => {
                for (int i = 0; i < ITER_POOL; i++)
                {
                    var a = new float[POOL_SIZE];
                    for (int j = 0; j < a.Length; j++) a[j] = j * 0.1f;
                    sumNew += a[POOL_SIZE - 1];
                }
            });

            double tPool = TimeMs(() => {
                for (int i = 0; i < ITER_POOL; i++)
                {
                    var a = ArrayPool<float>.Shared.Rent(POOL_SIZE);
                    try { for (int j = 0; j < POOL_SIZE; j++) a[j] = j * 0.1f; sumPool += a[POOL_SIZE - 1]; }
                    finally { ArrayPool<float>.Shared.Return(a); }
                }
            });

            // stackalloc requires a constant size — separate method (C# compiler restriction)
            double tStack = BenchStackalloc(ref sumStack);

            GC.KeepAlive(sumNew); GC.KeepAlive(sumPool); GC.KeepAlive(sumStack);

            _row.Pool_NewArray_ms = tNew;
            _row.Pool_ArrayPool_ms = tPool;
            _row.Pool_Stackalloc_ms = tStack;

            double usPer(double ms) => ms * 1000.0 / ITER_POOL;
            L($"  {"Method",-32}  {"ms",8}  {"µs/iter",10}  Note");
            L($"  {new string('─', 66)}");
            L($"  {"new float[" + POOL_SIZE + "]",-32}  {tNew,8:F2}  {usPer(tNew),10:F2}  GC allocation every time");
            L($"  {"ArrayPool<float>.Rent(" + POOL_SIZE + ")",-32}  {tPool,8:F2}  {usPer(tPool),10:F2}  buffer reuse");
            L($"  {"stackalloc float[256]",-32}  {tStack,8:F2}  {usPer(tStack),10:F2}  stack, no GC, ≤ ~1 MB");
            L($"  NOTE: ArrayPool is {tNew / Math.Max(tPool, 0.001):F1}× faster than new[]. stackalloc is best for small buffers.");
        }

        // stackalloc requires a constant size — separate method (C# compiler restriction)
        private static double BenchStackalloc(ref float sum)
        {
            const int STACK_SIZE = 256;
            float acc = 0;
            { Span<float> w = stackalloc float[STACK_SIZE]; for (int j = 0; j < w.Length; j++) w[j] = j * 0.1f; acc += w[0]; }
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < ITER_POOL; i++)
            {
                Span<float> a = stackalloc float[STACK_SIZE];
                for (int j = 0; j < a.Length; j++) a[j] = j * 0.1f;
                acc += a[STACK_SIZE - 1];
            }
            sw.Stop();
            sum = acc;
            return sw.Elapsed.TotalMilliseconds;
        }

        // =============================================================
        //  12. Type check — is / as / Category.Id
        // =============================================================
        private void MeasureTypeCast(Wall wall)
        {
            L($"  Check: is Element a wall  |  iterations: {ITER_CAST:N0}\n");

            Element elem = wall;
            int wallCatId = (int)BuiltInCategory.OST_Walls;
            bool r1 = false, r4 = false, r5 = false;
            Wall r2 = null, r3 = null;

            double tIs = TimeMs(() => { for (int i = 0; i < ITER_CAST; i++) r1 = elem is Wall; });
            double tAs = TimeMs(() => { for (int i = 0; i < ITER_CAST; i++) r2 = elem as Wall; });
            double tIsPattern = TimeMs(() => { for (int i = 0; i < ITER_CAST; i++) { if (elem is Wall w) r3 = w; } });
            double tGetType = TimeMs(() => { for (int i = 0; i < ITER_CAST; i++) r5 = elem.GetType() == typeof(Wall); });
            double tCat = TimeMs(() => { for (int i = 0; i < ITER_CAST; i++) r4 = elem.Category?.Id.IntegerValue == wallCatId; });

            GC.KeepAlive(r1); GC.KeepAlive(r2); GC.KeepAlive(r3); GC.KeepAlive(r4); GC.KeepAlive(r5);

            _row.Cast_Is_ms = tIs;
            _row.Cast_As_ms = tAs;
            _row.Cast_IsPattern_ms = tIsPattern;
            _row.Cast_GetType_ms = tGetType;
            _row.Cast_Category_ms = tCat;

            L($"  {"Method",-36}  {"ms",8}  {"ns/call",10}  Note");
            L($"  {new string('─', 74)}");
            L($"  {"elem is Wall",-36}  {tIs,8:F2}  {NsPerOp(tIs, ITER_CAST),10:F3}  isinst — considers inheritance");
            L($"  {"elem as Wall  (+ null-check)",-36}  {tAs,8:F2}  {NsPerOp(tAs, ITER_CAST),10:F3}  isinst — no exception");
            L($"  {"if (elem is Wall w)",-36}  {tIsPattern,8:F2}  {NsPerOp(tIsPattern, ITER_CAST),10:F3}  C# 7 pattern — single isinst");
            L($"  {"elem.GetType() == typeof(Wall)",-36}  {tGetType,8:F2}  {NsPerOp(tGetType, ITER_CAST),10:F3}  exact match, no inheritance");
            L($"  {"elem.Category?.Id.IntegerValue",-36}  {tCat,8:F2}  {NsPerOp(tCat, ITER_CAST),10:F3}  Revit managed object, slower");
            L($"  NOTE: is/as/pattern all compile to a single isinst IL opcode — fastest options.");
            L($"         Category.Id is slower: managed Revit wrapper call on every iteration.");
        }

        // =============================================================
        //  CSV — cumulative results file
        // =============================================================

        /// <summary>
        /// Appends one result row to Documents\RevitBenchmarks\BenchmarkResults.csv.
        /// Creates the file with a header row if it does not yet exist.
        /// </summary>
        /// <returns>Full path to the CSV file.</returns>
        private string SaveCsv()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "RevitBenchmarks");
            Directory.CreateDirectory(folder);

            string path = Path.Combine(folder, "BenchmarkResults.csv");
            bool isNew = !File.Exists(path);

            using (var writer = new StreamWriter(path, append: true, Encoding.UTF8))
            {
                if (isNew)
                    writer.WriteLine(BenchmarkRow.CsvHeader());

                writer.WriteLine(_row.ToCsvLine());
            }

            return path;
        }

        // =============================================================
        //  Log formatting helpers
        // =============================================================
        private void Header(string title) =>
            _log.AppendLine($"╔{_headerLine}╗\n║  {title,-54}║\n╚{_headerLine}╝\n");

        private void Section(string title) =>
            _log.AppendLine($"\n┌─ {title} " + new string('─', Math.Max(0, 50 - title.Length)) + "\n");

        private void L(string text = "") => _log.AppendLine(text);
    }
}