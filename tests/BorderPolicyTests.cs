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
        Check(BorderPolicy.Warning>BorderPolicy.Arrival && BorderPolicy.Arrival>BorderPolicy.Trigger,"margins ordered");
        Check(LandingRules.Ground(false,false,true,0,false,-128),"solid terrain accepted");
        Check(!LandingRules.Ground(false,false,true,0,false,127),"air density rejected");
        Check(!LandingRules.Ground(false,false,true,0,false,0),"ambiguous density rejected");
        Check(!LandingRules.Ground(true,false,true,0,false,-128),"missing ground rejected");
        Check(!LandingRules.Ground(false,true,true,0,false,-128),"water rejected");
        Check(!LandingRules.Ground(false,false,false,0,false,-128),"props and structures rejected");
        Check(!LandingRules.Ground(false,false,true,1,false,-128),"damaging ground rejected");
        Check(!LandingRules.Ground(false,false,true,0,true,-128),"ground buffs rejected");
        Console.WriteLine("Border policy and landing rules: PASS ("+assertions+" assertions)");
    }
}
