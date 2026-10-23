using System;
using System.IO;
using SevenDaysBeyond;
class QuestBookStateTests
{
    static int count;
    static void Check(bool x,string message) { count++; if(!x) throw new Exception(message); }
    static void Reject(Action a,string message) { try { a(); } catch { count++; return; } throw new Exception(message); }
    static void Main()
    {
        var b=new QuestBookState("EOS_player");
        b.Pages.Add("West",new QuestPage {Map="West",Journal=Convert.ToBase64String(new byte[]{1,2,3}),Summary=new[]{"sameQuest [Active] count=1", "escaped <&> text"}});
        b.Pages.Add("East",new QuestPage {Map="East",Journal=Convert.ToBase64String(new byte[]{4,5}),Summary=new[]{"sameQuest [Active] count=2"}});
        var copy=QuestBookState.Parse(b.Serialize(),b.Player);
        Check(copy.Pages.Count==2,"two map pages retained");
        Check(copy.Pages["West"].Journal!=copy.Pages["East"].Journal,"same coordinate or quest ID never merges map data");
        Check(copy.Pages["West"].Summary[1]=="escaped <&> text","XML-safe summary roundtrip");
        Reject(()=>QuestBookState.Parse(b.Serialize(),"anotherPlayer"),"wrong character rejected");
        Reject(()=>QuestBookState.Parse("<!DOCTYPE x [<!ENTITY e SYSTEM 'file:///etc/passwd'>]><QuestBook version='1' player='p'/>","p"),"DTD rejected");
        Reject(()=>QuestBookState.Parse(b.Serialize().Replace("AQID","!not-base64!"),b.Player),"bad native data rejected");
        var t=new TravelQuest {Home="West",Target="NorthEast",Deadline=100};
        Check(!t.Advance("West",1) && t.Stage==0,"home does not count as arrival");
        Check(!t.Advance("East",10),"intermediate or wrong map does not satisfy destination");
        Check(t.Advance("NorthEast",11) && t.Stage==1,"target map advances first leg");
        Check(!t.Advance("NorthEast",12),"remaining in target does not repeat credit");
        b.Test=t;copy=QuestBookState.Parse(b.Serialize(),b.Player);
        Check(copy.Test.Stage==1 && copy.Test.Deadline==100,"partial route and deadline survive restart");
        Check(copy.Test.Advance("West",99) && copy.Test.Status=="complete","return before deadline completes");
        Check(!copy.Test.Advance("West",200) && copy.Test.Status=="complete","completed result never expires or pays again");
        var expired=new TravelQuest {Home="West",Target="East",Stage=1,Deadline=100};
        Check(expired.Advance("West",100) && expired.Status=="expired","expiration wins at exact deadline");
        Check(!expired.Advance("West",99),"expired result cannot be resurrected");
        var cancelled=new TravelQuest {Home="West",Target="East",Deadline=100,Status="cancelled"};
        Check(!cancelled.Advance("East",5),"cancelled quest cannot gain progress");
        string dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));string file=Path.Combine(dir,"book.xml");
        try
        {
            b.Save(file);b.Test.Stage=0;b.Save(file);
            Check(File.Exists(file+".bak"),"previous version retained");
            Check(QuestBookState.Parse(File.ReadAllText(file+".bak"),b.Player).Test.Stage==1,"backup retains prior progress");
            Check(QuestBookState.Parse(File.ReadAllText(file),b.Player).Test.Stage==0,"atomic update reads current progress");
        }
        finally { Directory.Delete(dir,true); }
        Console.WriteLine("Quest book rules: PASS ("+count+" assertions)");
    }
}
