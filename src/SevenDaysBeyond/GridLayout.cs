using System;
using System.Collections.Generic;
using System.Linq;

namespace SevenDaysBeyond
{
    internal sealed class GridCell
    {
        internal string Name;
        internal int X, Z;
        internal GridCell(string name, int x, int z) { Name=name; X=x; Z=z; }
    }
    internal sealed class GridLayout
    {
        internal readonly GridCell[] Cells;
        internal GridLayout(IEnumerable<GridCell> cells)
        {
            Cells=cells.ToArray();
            if(Cells.Length<1 || Cells.Length>64) throw new ArgumentException("Grid must contain 1-64 cells.");
            if(Cells.Any(c=>string.IsNullOrWhiteSpace(c.Name) || c.Name.Length>40 || c.Name.Any(ch=>!char.IsLetterOrDigit(ch) && ch!='_' && ch!='-') || Math.Abs((long)c.X)>1000 || Math.Abs((long)c.Z)>1000)
                || Cells.Select(c=>c.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=Cells.Length
                || Cells.Select(c=>c.X+":"+c.Z).Distinct().Count()!=Cells.Length) throw new ArgumentException("Invalid or duplicate grid cell.");
        }
        internal string Destination(string source, BorderEdge edge)
        {
            GridCell c=Cells.SingleOrDefault(v=>v.Name==source);
            if(c==null) return null;
            int x=c.X+(edge==BorderEdge.East?1:edge==BorderEdge.West?-1:0);
            int z=c.Z+(edge==BorderEdge.North?1:edge==BorderEdge.South?-1:0);
            return Cells.Where(v=>v.X==x && v.Z==z).Select(v=>v.Name).SingleOrDefault();
        }
        internal BorderEdge FirstExit(string source)
        {
            foreach(BorderEdge edge in new[]{BorderEdge.East,BorderEdge.West,BorderEdge.North,BorderEdge.South})
                if(Destination(source,edge)!=null) return edge;
            throw new InvalidOperationException("This cell has no linked neighbor.");
        }
        internal static GridLayout TwoWorlds()
        { return new GridLayout(new[]{new GridCell("West",0,0),new GridCell("East",1,0)}); }
    }
}
