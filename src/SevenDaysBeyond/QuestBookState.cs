using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;

namespace SevenDaysBeyond
{
    internal sealed class QuestPage
    {
        internal string Map, Journal;
        internal string[] Summary=new string[0];
    }
    internal sealed class TravelQuest
    {
        internal string Home, Target, Status="active";
        internal int Stage;
        internal ulong Deadline;
        internal bool Advance(string map,ulong now)
        {
            if(Status!="active") return false;
            if(now>=Deadline) { Status="expired"; return true; }
            if(Stage==0 && map==Target) { Stage=1; return true; }
            if(Stage==1 && map==Home) { Stage=2; Status="complete"; return true; }
            return false;
        }
    }
    // Character-owned collection of map journals. Never reinterpret a POI coordinate in another map.
    internal sealed class QuestBookState
    {
        internal string Player;
        internal readonly Dictionary<string,QuestPage> Pages=new Dictionary<string,QuestPage>(StringComparer.Ordinal);
        internal TravelQuest Test;
        internal QuestBookState(string player) { Player=player; }
        internal string Serialize()
        {
            var doc=new XmlDocument {XmlResolver=null};
            XmlElement root=doc.CreateElement("QuestBook"); root.SetAttribute("version","1"); root.SetAttribute("player",Player); doc.AppendChild(root);
            foreach(var page in Pages.Values.OrderBy(p=>p.Map,StringComparer.Ordinal))
            {
                XmlElement e=doc.CreateElement("Map"); e.SetAttribute("name",page.Map);
                XmlElement data=doc.CreateElement("Journal"); data.InnerText=page.Journal; e.AppendChild(data);
                foreach(string line in page.Summary) { XmlElement q=doc.CreateElement("Quest"); q.InnerText=line; e.AppendChild(q); }
                root.AppendChild(e);
            }
            if(Test!=null)
            {
                XmlElement t=doc.CreateElement("TravelTest"); t.SetAttribute("home",Test.Home); t.SetAttribute("target",Test.Target);
                t.SetAttribute("status",Test.Status); t.SetAttribute("stage",Test.Stage.ToString(CultureInfo.InvariantCulture));
                t.SetAttribute("deadline",Test.Deadline.ToString(CultureInfo.InvariantCulture)); root.AppendChild(t);
            }
            return doc.OuterXml;
        }
        internal static QuestBookState Parse(string xml,string player)
        {
            if(xml.Length>16*1024*1024) throw new InvalidDataException("Quest book too large.");
            var doc=new XmlDocument {XmlResolver=null};
            using(var r=XmlReader.Create(new StringReader(xml),new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null})) doc.Load(r);
            var root=doc.DocumentElement;
            if(root==null || root.Name!="QuestBook" || root.GetAttribute("version")!="1" || root.GetAttribute("player")!=player) throw new InvalidDataException("Quest book identity/version mismatch.");
            var book=new QuestBookState(player);
            foreach(XmlElement e in root.SelectNodes("Map"))
            {
                string name=e.GetAttribute("name"), journal=e.SelectSingleNode("Journal")?.InnerText;
                if(string.IsNullOrWhiteSpace(name) || name.Length>40 || journal==null || journal.Length>4*1024*1024 || Convert.FromBase64String(journal).Length>2*1024*1024 || book.Pages.Count>=64 || book.Pages.ContainsKey(name)) throw new InvalidDataException("Invalid quest page.");
                string[] lines=e.SelectNodes("Quest").Cast<XmlElement>().Select(q=>q.InnerText).ToArray();
                if(lines.Length>4096 || lines.Any(l=>l.Length>2048)) throw new InvalidDataException("Quest summaries too large.");
                book.Pages.Add(name,new QuestPage {Map=name,Journal=journal,Summary=lines});
            }
            XmlElement t=root.SelectSingleNode("TravelTest") as XmlElement;
            if(t!=null)
            {
                int stage=int.Parse(t.GetAttribute("stage"),CultureInfo.InvariantCulture); string status=t.GetAttribute("status");
                if(stage<0 || stage>2 || !new[]{"active","complete","expired","cancelled"}.Contains(status)) throw new InvalidDataException("Invalid travel test.");
                string home=t.GetAttribute("home"),target=t.GetAttribute("target");
                if(string.IsNullOrWhiteSpace(home) || string.IsNullOrWhiteSpace(target) || home==target) throw new InvalidDataException("Invalid travel route.");
                book.Test=new TravelQuest {Home=home,Target=target,Stage=stage,Status=status,Deadline=ulong.Parse(t.GetAttribute("deadline"),CultureInfo.InvariantCulture)};
            }
            return book;
        }
        internal void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string text=Serialize();
            if(File.Exists(path) && File.ReadAllText(path)==text) return;
            string temporary=path+"."+Guid.NewGuid().ToString("N")+".new";
            try
            {
                using(var f=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                using(var writer=new StreamWriter(f)) { writer.Write(text); writer.Flush(); f.Flush(true); }
                if(File.Exists(path)) File.Replace(temporary,path,path+".bak"); else File.Move(temporary,path);
            }
            finally { if(File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
