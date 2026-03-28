using System;
using System.Globalization;

namespace RevitBenchmark
{
    internal sealed class BenchmarkRow
    {
        // Run metadata
        public DateTime RunDateTime;
        public int WallId;
        public string WallTypeName;

        // 1. Memory sizes (bytes)
        public int Sz_RevitProxy_bytes;
        public int Sz_ClassDouble_bytes;
        public int Sz_ClassFloat_bytes;
        public int Sz_Struct_bytes;
        public int Sz_GeomStruct_bytes;
        public int Sz_MetaStruct_bytes;

        // 2. Function A: coordinate transform + √ (ms, 1 000 000 iter)
        public double FuncA_RevitAPI_ms;
        public double FuncA_ClassDouble_ms;
        public double FuncA_ClassFloat_ms;
        public double FuncA_Struct_ms;
        public double FuncA_GeomStruct_ms;
        public double FuncA_MetaStruct_ms;

        // 3. Function B: area calculation S=L×H−W×Base (ms, 10 000 000 iter)
        public double FuncB_ClassDouble_ms;
        public double FuncB_ClassFloat_ms;
        public double FuncB_Struct_ms;
        public double FuncB_GeomStruct_ms;
        public double FuncB_MetaStruct_ms;

        // 4. Boxing/Unboxing: value→object→value (ms, 1 000 000 iter)
        public double Box_Struct_ms;
        public double Box_GeomStruct_ms;
        public double Box_MetaStruct_ms;

        // 5. Span<T>/Memory<T> array traversal (ms, 500 000 elements)
        public double Span_Struct_Array_ms;
        public double Span_Struct_Span_ms;
        public double Span_Struct_Memory_ms;
        public double Span_Geom_Array_ms;
        public double Span_Geom_Span_ms;
        public double Span_Geom_Memory_ms;
        public double Span_Meta_Array_ms;
        public double Span_Meta_Span_ms;
        public double Span_Meta_Memory_ms;

        // 6. List<T>: fill and traverse (ms, 1 000 000 elements)
        public double List_ClassDouble_Fill_ms;
        public double List_ClassDouble_Trav_ms;
        public double List_ClassFloat_Fill_ms;
        public double List_ClassFloat_Trav_ms;
        public double List_Struct_Fill_ms;
        public double List_Struct_Trav_ms;
        public double List_GeomStruct_Fill_ms;
        public double List_GeomStruct_Trav_ms;
        public double List_MetaStruct_Fill_ms;
        public double List_MetaStruct_Trav_ms;
        public double List_Tuple_Fill_ms;
        public double List_Tuple_Trav_ms;

        // 7. A*: one step (ns/step, 10 000 reps)
        public double AStar_ClassDouble_nsPerStep;
        public double AStar_ClassFloat_nsPerStep;
        public double AStar_Struct_nsPerStep;
        public double AStar_GeomStruct_nsPerStep;

        // 8. Revit parameter access (ms, 10 000 iter)
        public double Param_BIP_ms;
        public double Param_Lookup_ms;
        public double Param_Cached_ms;

        // 9. FilteredElementCollector (ms, one call each)
        public double Coll_Linq_ms;
        public double Coll_FastFilter_ms;
        public double Coll_ById_ms;

        // 10. Point transform (ms, 1 000 000 iter)
        public double Xform_Revit_ms;
        public double Xform_Matrix_ms;

        // 11. Buffer allocation (ms, 500 iter × 4096 elements)
        public double Pool_NewArray_ms;
        public double Pool_ArrayPool_ms;
        public double Pool_Stackalloc_ms;

        // 12. Type check (ms, 5 000 000 iter)
        public double Cast_Is_ms;
        public double Cast_As_ms;
        public double Cast_IsPattern_ms;
        public double Cast_GetType_ms;
        public double Cast_Category_ms;

        // Column order must match ToCsvLine
        public static string CsvHeader() => string.Join(",",
            "RunDateTime", "WallId", "WallTypeName",
            "Sz_RevitProxy_bytes", "Sz_ClassDouble_bytes", "Sz_ClassFloat_bytes",
            "Sz_Struct_bytes", "Sz_GeomStruct_bytes", "Sz_MetaStruct_bytes",
            "FuncA_RevitAPI_ms", "FuncA_ClassDouble_ms", "FuncA_ClassFloat_ms",
            "FuncA_Struct_ms", "FuncA_GeomStruct_ms", "FuncA_MetaStruct_ms",
            "FuncB_ClassDouble_ms", "FuncB_ClassFloat_ms", "FuncB_Struct_ms",
            "FuncB_GeomStruct_ms", "FuncB_MetaStruct_ms",
            "Box_Struct_ms", "Box_GeomStruct_ms", "Box_MetaStruct_ms",
            "Span_Struct_Array_ms", "Span_Struct_Span_ms", "Span_Struct_Memory_ms",
            "Span_Geom_Array_ms", "Span_Geom_Span_ms", "Span_Geom_Memory_ms",
            "Span_Meta_Array_ms", "Span_Meta_Span_ms", "Span_Meta_Memory_ms",
            "List_ClassDouble_Fill_ms", "List_ClassDouble_Trav_ms",
            "List_ClassFloat_Fill_ms", "List_ClassFloat_Trav_ms",
            "List_Struct_Fill_ms", "List_Struct_Trav_ms",
            "List_GeomStruct_Fill_ms", "List_GeomStruct_Trav_ms",
            "List_MetaStruct_Fill_ms", "List_MetaStruct_Trav_ms",
            "List_Tuple_Fill_ms", "List_Tuple_Trav_ms",
            "AStar_ClassDouble_nsPerStep", "AStar_ClassFloat_nsPerStep",
            "AStar_Struct_nsPerStep", "AStar_GeomStruct_nsPerStep",
            "Param_BIP_ms", "Param_Lookup_ms", "Param_Cached_ms",
            "Coll_Linq_ms", "Coll_FastFilter_ms", "Coll_ById_ms",
            "Xform_Revit_ms", "Xform_Matrix_ms",
            "Pool_NewArray_ms", "Pool_ArrayPool_ms", "Pool_Stackalloc_ms",
            "Cast_Is_ms", "Cast_As_ms", "Cast_IsPattern_ms",
            "Cast_GetType_ms", "Cast_Category_ms"
        );

        public string ToCsvLine()
        {
            var ci = CultureInfo.InvariantCulture;
            string F(double v) => v.ToString("F4", ci);
            string I(int v) => v.ToString(ci);

            return string.Join(",",
                RunDateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                I(WallId), CsvEscape(WallTypeName),
                I(Sz_RevitProxy_bytes), I(Sz_ClassDouble_bytes), I(Sz_ClassFloat_bytes),
                I(Sz_Struct_bytes), I(Sz_GeomStruct_bytes), I(Sz_MetaStruct_bytes),
                F(FuncA_RevitAPI_ms), F(FuncA_ClassDouble_ms), F(FuncA_ClassFloat_ms),
                F(FuncA_Struct_ms), F(FuncA_GeomStruct_ms), F(FuncA_MetaStruct_ms),
                F(FuncB_ClassDouble_ms), F(FuncB_ClassFloat_ms), F(FuncB_Struct_ms),
                F(FuncB_GeomStruct_ms), F(FuncB_MetaStruct_ms),
                F(Box_Struct_ms), F(Box_GeomStruct_ms), F(Box_MetaStruct_ms),
                F(Span_Struct_Array_ms), F(Span_Struct_Span_ms), F(Span_Struct_Memory_ms),
                F(Span_Geom_Array_ms), F(Span_Geom_Span_ms), F(Span_Geom_Memory_ms),
                F(Span_Meta_Array_ms), F(Span_Meta_Span_ms), F(Span_Meta_Memory_ms),
                F(List_ClassDouble_Fill_ms), F(List_ClassDouble_Trav_ms),
                F(List_ClassFloat_Fill_ms), F(List_ClassFloat_Trav_ms),
                F(List_Struct_Fill_ms), F(List_Struct_Trav_ms),
                F(List_GeomStruct_Fill_ms), F(List_GeomStruct_Trav_ms),
                F(List_MetaStruct_Fill_ms), F(List_MetaStruct_Trav_ms),
                F(List_Tuple_Fill_ms), F(List_Tuple_Trav_ms),
                F(AStar_ClassDouble_nsPerStep), F(AStar_ClassFloat_nsPerStep),
                F(AStar_Struct_nsPerStep), F(AStar_GeomStruct_nsPerStep),
                F(Param_BIP_ms), F(Param_Lookup_ms), F(Param_Cached_ms),
                F(Coll_Linq_ms), F(Coll_FastFilter_ms), F(Coll_ById_ms),
                F(Xform_Revit_ms), F(Xform_Matrix_ms),
                F(Pool_NewArray_ms), F(Pool_ArrayPool_ms), F(Pool_Stackalloc_ms),
                F(Cast_Is_ms), F(Cast_As_ms), F(Cast_IsPattern_ms),
                F(Cast_GetType_ms), F(Cast_Category_ms)
            );
        }

        // RFC-4180: escape fields containing comma, quote, or line break
        private static string CsvEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
    }
}