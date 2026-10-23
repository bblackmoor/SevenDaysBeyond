using System;
using System.Globalization;
using System.IO;
using SevenDaysBeyond;
class CalendarStateTests
{
    static int assertions;
    static void Check(bool value,string why){assertions++;if(!value)throw new Exception(why);}
    static void Reject(Action action,string why){try{action();}catch(Exception){assertions++;return;}throw new Exception(why);}
    static void Main()
    {
        var c=new CalendarState(100);
        c.Advance(1,6,true); Check(c.Ticks==106,"normal rate");
        c.Advance(99999,6,false);Check(c.Ticks==106,"offline interval ignored");
        c.Advance(1,0,true);Check(c.Ticks==106,"zero configured speed");
        c.Advance(0.25,6,true);Check(c.Ticks==107 && c.Fraction==0.5,"fractional accumulation");
        c.Advance(0.25,6,true);Check(c.Ticks==109 && c.Fraction==0,"fraction carried once");
        Check(!c.Expired(110) && c.Expired(109) && c.Expired(108),"deadline inclusive and independent of map");
        var many=new CalendarState(0);
        for(int i=0;i<64;i++) Check(many.Ticks==0,"reading a world never advances calendar");
        many.Advance(1,10,true);Check(many.Ticks==10,"advance once regardless of world count");
        Reject(()=>new CalendarState(0,-1),"negative fraction");Reject(()=>new CalendarState(0,1),"fraction outside unit interval");
        Reject(()=>new CalendarState(0,double.NaN),"NaN fraction");Reject(()=>c.Advance(-1,6,true),"negative delta");
        Reject(()=>c.Advance(double.PositiveInfinity,6,true),"infinite delta");Reject(()=>c.Advance(1,double.NaN,true),"NaN rate");
        Reject(()=>c.Advance(1,-1,true),"negative rate");Reject(()=>new CalendarState(ulong.MaxValue-1).Advance(2,2,true),"overflow");
        var culture=CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("de-DE");
        try
        {
            var original=new CalendarState(17799,0.375);
            var restored=CalendarState.Parse(original.Serialize());
            Check(restored.Ticks==17799 && restored.Fraction==0.375,"culture-independent roundtrip");
            string dir=Path.Combine(Path.GetTempPath(),"sdb-calendar-"+Guid.NewGuid().ToString("N"));
            string path=Path.Combine(dir,"calendar.xml");
            try
            {
                original.Save(path); original.Advance(1,6,true); original.Save(path);
                var disk=CalendarState.Parse(File.ReadAllText(path));
                Check(disk.Ticks==original.Ticks && disk.Fraction==original.Fraction,"atomic persisted replacement");
                Check(CalendarState.Parse(File.ReadAllText(path+".bak")).Ticks==17799,"previous checkpoint retained");
                disk.Advance(3600,6,false);Check(disk.Ticks==original.Ticks,"restart does not simulate offline time");
            }
            finally{Directory.Delete(dir,true);}
        }
        finally{CultureInfo.CurrentCulture=culture;}
        Reject(()=>CalendarState.Parse("<wrong/>"),"unknown schema rejected");
        Reject(()=>CalendarState.Parse("<SevenDaysBeyondCalendar version='2' ticks='1' fraction='0'/>"),"newer version rejected");
        Reject(()=>CalendarState.Parse("<SevenDaysBeyondCalendar version='1' ticks='1' fraction='NaN'/>"),"bad checkpoint rejected");
        Reject(()=>CalendarState.Parse("<!DOCTYPE x [<!ENTITY a SYSTEM 'file:///etc/passwd'>]><x>&a;</x>"),"external entity prohibited");
        Console.WriteLine("Calendar state: PASS ("+assertions+" assertions)");
    }
}
