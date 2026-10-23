using System;

namespace SevenDaysBeyond
{
    internal static class LandingRules
    {
        // The inspected game's MarchingCubes uses negative density for solid terrain.
        internal static bool Ground(bool air, bool water, bool terrain, float damage, bool buffs, int density)
        { return !air && !water && terrain && damage <= 0 && !buffs && density < 0; }
    }

    internal enum BorderEdge { West, East, South, North }

    internal struct MapBounds
    {
        public float MinX, MaxX, MinZ, MaxZ;
        public MapBounds(float minX, float maxX, float minZ, float maxZ)
        { MinX = minX; MaxX = maxX; MinZ = minZ; MaxZ = maxZ; }
        public bool Valid { get { return MaxX - MinX > 2 * BorderPolicy.Warning && MaxZ - MinZ > 2 * BorderPolicy.Warning; } }
    }

    // Pure geometry: no world selection, networking, or save access.
    internal static class BorderPolicy
    {
        internal static GridLayout Grid = GridLayout.TwoWorlds();
        public const float Trigger = 500, Arrival = 600;
        public const float EarlyWarningDistance = 50, CloseWarningDistance = 10;
        public const float Warning = Trigger + EarlyWarningDistance;
        // Distances are measured from the playable travel line, not the raw map extent.
        public static int WarningLevel(float mapEdgeDistance)
        {
            float remaining = mapEdgeDistance - Trigger;
            return remaining > EarlyWarningDistance ? 0 : remaining > CloseWarningDistance ? 1 : 2;
        }
        public static BorderEdge Nearest(MapBounds b, float x, float z, out float distance)
        {
            BorderEdge edge = BorderEdge.West;
            distance = x - b.MinX;
            if (b.MaxX - x < distance) { distance = b.MaxX - x; edge = BorderEdge.East; }
            if (z - b.MinZ < distance) { distance = z - b.MinZ; edge = BorderEdge.South; }
            if (b.MaxZ - z < distance) { distance = b.MaxZ - z; edge = BorderEdge.North; }
            return edge;
        }
        public static string Destination(string source, BorderEdge edge)
        {
            return Grid.Destination(source,edge);
        }
        public static float Clamp(float value, float min, float max) { return Math.Max(min, Math.Min(max, value)); }
        public static void Inland(MapBounds b, float x, float z, float margin, out float tx, out float tz)
        {
            tx = Clamp(x, b.MinX + margin, b.MaxX - margin);
            tz = Clamp(z, b.MinZ + margin, b.MaxZ - margin);
        }
        public static void ArrivalPoint(MapBounds source, MapBounds target, BorderEdge exit, float z, out float x, out float tz)
        { ArrivalPoint(source,target,exit,0,z,out x,out tz); }
        public static void ArrivalPoint(MapBounds source, MapBounds target, BorderEdge exit, float sourceX, float sourceZ, out float x, out float tz)
        {
            // Preserve latitude proportionally, allowing future maps with unequal extents.
            bool horizontal=exit==BorderEdge.East || exit==BorderEdge.West;
            float fraction = horizontal?Clamp((sourceZ-source.MinZ)/(source.MaxZ-source.MinZ),0,1)
                :Clamp((sourceX-source.MinX)/(source.MaxX-source.MinX),0,1);
            x=horizontal?(exit==BorderEdge.East?target.MinX+Arrival:target.MaxX-Arrival)
                :Clamp(target.MinX+fraction*(target.MaxX-target.MinX),target.MinX+Arrival,target.MaxX-Arrival);
            tz=horizontal?Clamp(target.MinZ+fraction*(target.MaxZ-target.MinZ),target.MinZ+Arrival,target.MaxZ-Arrival)
                :(exit==BorderEdge.North?target.MinZ+Arrival:target.MaxZ-Arrival);
        }
    }
}
