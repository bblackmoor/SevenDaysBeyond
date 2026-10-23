using System;
using SevenDaysBeyond;

internal static class BorderPolicyTests
{
    private static int assertions;
    private static void Check(bool condition, string message)
    { assertions++; if (!condition) throw new Exception(message); }
    private static void Near(float actual, float expected, string message)
    { Check(Math.Abs(actual - expected) < 0.01, message); }
    public static void Main()
    {
        var b = new MapBounds(-3072, 3072, -3072, 3072);
        var larger = new MapBounds(-4096, 4096, -4096, 4096);
        Check(b.Valid, "6km map valid");
        Check(!new MapBounds(-500,500,-500,500).Valid, "small map refused");
        Check(!new MapBounds(1,-1,1,-1).Valid, "inverted map refused");
        float d,x,z;
        Check(BorderPolicy.Nearest(b, 2600, 0, out d) == BorderEdge.East, "east detected"); Near(d,472,"east distance");
        Check(BorderPolicy.Nearest(b, -2600, 0, out d) == BorderEdge.West, "west detected");
        Check(BorderPolicy.Nearest(b, 0, -2600, out d) == BorderEdge.South, "south detected");
        Check(BorderPolicy.Nearest(b, 0, 2600, out d) == BorderEdge.North, "north detected");
        Check(BorderPolicy.Nearest(b, 4000, 0, out d) == BorderEdge.East && d < 0, "outside map guarded");
        foreach(BorderEdge edge in Enum.GetValues(typeof(BorderEdge)))
        {
            Check(BorderPolicy.Destination("West",edge) == (edge==BorderEdge.East?"East":null),"only West east linked");
            Check(BorderPolicy.Destination("East",edge) == (edge==BorderEdge.West?"West":null),"only East west linked");
            Check(BorderPolicy.Destination("Other",edge)==null,"unknown world not linked");
        }
        BorderPolicy.ArrivalPoint(b,b,BorderEdge.East,100,out x,out z);
        Near(x,-2472,"east exit enters west side"); Near(z,100,"latitude preserved");
        BorderPolicy.ArrivalPoint(b,b,BorderEdge.West,-100,out x,out z);
        Near(x,2472,"west exit enters east side"); Near(z,-100,"reverse latitude");
        BorderPolicy.ArrivalPoint(b,larger,BorderEdge.East,1536,out x,out z);
        Near(z,2048,"unequal-map latitude mapping");
        for(int latitude=-4000;latitude<=4000;latitude+=17)
        {
            BorderPolicy.ArrivalPoint(b,b,BorderEdge.East,latitude,out x,out z);
            BorderPolicy.Nearest(b,x,z,out d);
            Check(d>=BorderPolicy.Arrival,"arrival cannot immediately retrigger, including corners");
            Check(d>BorderPolicy.Trigger,"arrival outside crossing zone");
        }
        BorderPolicy.Inland(b,4000,-4000,540,out x,out z);
        Near(x,2532,"retreat x clamped"); Near(z,-2532,"retreat z clamped");
        Check(BorderPolicy.Arrival>BorderPolicy.Warning && BorderPolicy.Warning>BorderPolicy.Trigger,"arrival outside warning and crossing zones");
        Check(BorderPolicy.WarningLevel(550.01f)==0,"no warning beyond 50m from travel line");
        Check(BorderPolicy.WarningLevel(550)==1,"early warning at exactly 50m");
        Check(BorderPolicy.WarningLevel(510.01f)==1,"early stage until 10m");
        Check(BorderPolicy.WarningLevel(510)==2,"close warning at exactly 10m");
        Check(BorderPolicy.WarningLevel(500)==2,"crossing line remains inside close stage");
        Check(LandingRules.Ground(false,false,true,0,false,-128),"solid terrain accepted");
        Check(!LandingRules.Ground(false,false,true,0,false,127),"air density rejected");
        Check(!LandingRules.Ground(false,false,true,0,false,0),"ambiguous density rejected");
        Check(!LandingRules.Ground(true,false,true,0,false,-128),"missing ground rejected");
        Check(!LandingRules.Ground(false,true,true,0,false,-128),"water rejected");
        Check(!LandingRules.Ground(false,false,false,0,false,-128),"props and structures rejected");
        Check(!LandingRules.Ground(false,false,true,1,false,-128),"damaging ground rejected");
        Check(!LandingRules.Ground(false,false,true,0,true,-128),"ground buffs rejected");
        Check(SuspensionPolicy.MustRun(1,false,false,false,100,0),"occupied world wakes immediately");
        Check(SuspensionPolicy.MustRun(0,true,false,false,100,0),"joining client wakes destination before spawn");
        Check(SuspensionPolicy.MustRun(0,false,true,false,100,0),"transfer source/destination protected");
        Check(SuspensionPolicy.MustRun(0,false,false,true,100,0),"preflight observer protected");
        Check(SuspensionPolicy.MustRun(0,false,false,false,109.9f,100),"grace permits save and worker drain");
        Check(!SuspensionPolicy.MustRun(0,false,false,false,110,100),"empty world sleeps at grace deadline");
        Check(!SuspensionPolicy.MustRun(0,false,false,false,1000,100),"long idle stays asleep");
        Check(SuspensionPolicy.MustRun(0,false,true,false,1000,100),"new reservation wakes long-idle world");
        Check(SuspensionPolicy.MustRun(0,false,false,false,100,100),"release starts fresh grace");
        BorderPolicy.Grid=new GridLayout(new[]{new GridCell("West",0,0),new GridCell("East",1,0),new GridCell("NorthWest",0,1),new GridCell("NorthEast",1,1)});
        Check(BorderPolicy.Destination("West",BorderEdge.North)=="NorthWest","north link");
        Check(BorderPolicy.Destination("NorthWest",BorderEdge.South)=="West","south inverse");
        Check(BorderPolicy.Destination("East",BorderEdge.North)=="NorthEast","east column north link");
        Check(BorderPolicy.Destination("NorthEast",BorderEdge.West)=="NorthWest","upper row west link");
        Check(BorderPolicy.Destination("NorthWest",BorderEdge.North)==null,"outer north closed");
        Check(BorderPolicy.Destination("West",BorderEdge.South)==null,"outer south closed");
        foreach(BorderEdge edge in Enum.GetValues(typeof(BorderEdge)))
        for(int coordinate=-4000;coordinate<=4000;coordinate+=17)
        {
            BorderPolicy.ArrivalPoint(b,larger,edge,coordinate,coordinate,out x,out z);
            BorderPolicy.Nearest(larger,x,z,out d);
            Check(d>=BorderPolicy.Arrival,"all four arrival edges stay clear of corners");
            if(edge==BorderEdge.North) Near(z,larger.MinZ+BorderPolicy.Arrival,"north enters south");
            if(edge==BorderEdge.South) Near(z,larger.MaxZ-BorderPolicy.Arrival,"south enters north");
        }
        BorderPolicy.ArrivalPoint(b,larger,BorderEdge.North,1536,0,out x,out z);
        Near(x,2048,"north preserves longitude proportionally");
        bool duplicateRejected=false;
        try { new GridLayout(new[]{new GridCell("A",0,0),new GridCell("B",0,0)}); } catch(ArgumentException) {duplicateRejected=true;}
        Check(duplicateRejected,"overlapping grid cells rejected");
        duplicateRejected=false;
        try { new GridLayout(new[]{new GridCell("A",0,0),new GridCell("a",1,0)}); } catch(ArgumentException) {duplicateRejected=true;}
        Check(duplicateRejected,"ambiguous case-only map labels rejected");
        Console.WriteLine("Border policy and landing rules: PASS ("+assertions+" assertions)");
    }
}
