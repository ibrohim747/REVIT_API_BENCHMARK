using System;
using System.Runtime.InteropServices;

namespace RevitBenchmark
{
    public class WallDataClass
    {
        public int Id, TypeId, LevelId;
        public double StartX, StartY, StartZ, EndX, EndY, EndZ;
        public double Length, Height, Width, BaseOffset, TopOffset;
        public string TypeName;
    }

    public class WallDataClassFloat
    {
        public int Id, TypeId, LevelId;
        public float StartX, StartY, StartZ, EndX, EndY, EndZ;
        public float Length, Height, Width, BaseOffset, TopOffset;
        public string TypeName;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WallDataStruct
    {
        public int Id, TypeId, LevelId;
        public float StartX, StartY, StartZ, EndX, EndY, EndZ;
        public float Length, Height, Width, BaseOffset, TopOffset;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WallGeomStruct
    {
        public float StartX, StartY, StartZ, EndX, EndY, EndZ;
        public float Length, Height, Width;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WallMetaStruct
    {
        public int Id, TypeId, LevelId;
        public float BaseOffset, TopOffset;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XYZFloat
    {
        public float X, Y, Z;
        public XYZFloat(float x, float y, float z) { X = x; Y = y; Z = z; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Matrix4x4Float
    {
        public float M00, M01, M02, M03,
                     M10, M11, M12, M13,
                     M20, M21, M22, M23,
                     M30, M31, M32, M33;

        public XYZFloat TransformPoint(XYZFloat p) => new XYZFloat(
            M00 * p.X + M01 * p.Y + M02 * p.Z + M03,
            M10 * p.X + M11 * p.Y + M12 * p.Z + M13,
            M20 * p.X + M21 * p.Y + M22 * p.Z + M23);
    }

    public struct AStarNode
    {
        public float X, Y, G, H;
        public float F => G + H;
        public int ParentIdx;
    }

    public class AStarNodeClass
    {
        public double X, Y, G, H;
        public double F => G + H;
        public AStarNodeClass Parent;
    }
}