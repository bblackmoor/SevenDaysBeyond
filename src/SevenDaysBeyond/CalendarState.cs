using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace SevenDaysBeyond
{
    // One server calendar; deliberately contains no world or simulation-timer references.
    internal sealed class CalendarState
    {
        internal ulong Ticks { get; private set; }
        internal double Fraction { get; private set; }
        internal CalendarState(ulong ticks, double fraction = 0)
        {
            if(double.IsNaN(fraction) || double.IsInfinity(fraction) || fraction<0 || fraction>=1) throw new InvalidDataException("Invalid calendar fraction.");
            Ticks=ticks; Fraction=fraction;
        }
        internal void Advance(double seconds, double rate, bool occupied)
        {
            if(double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds<0 || double.IsNaN(rate) || double.IsInfinity(rate) || rate<0)
                throw new ArgumentOutOfRangeException("Invalid calendar step.");
            if(!occupied || rate==0) return;
            double delta=seconds*rate+Fraction;
            if(double.IsInfinity(delta) || delta>=ulong.MaxValue || delta>ulong.MaxValue-Ticks) throw new OverflowException("Calendar exhausted.");
            ulong whole=(ulong)Math.Floor(delta);
            Ticks=checked(Ticks+whole); Fraction=delta-whole;
        }
        internal bool Expired(ulong deadline) { return Ticks>=deadline; }
        internal string Serialize()
        {
            return "<SevenDaysBeyondCalendar version=\"1\" ticks=\""+Ticks.ToString(CultureInfo.InvariantCulture)
                +"\" fraction=\""+Fraction.ToString("R",CultureInfo.InvariantCulture)+"\" />";
        }
        internal static CalendarState Parse(string text)
        {
            var doc=new XmlDocument { XmlResolver=null };
            using(var reader=XmlReader.Create(new StringReader(text),new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null })) doc.Load(reader);
            var root=doc.DocumentElement;
            if(root==null || root.Name!="SevenDaysBeyondCalendar" || root.GetAttribute("version")!="1") throw new InvalidDataException("Unknown shared calendar format.");
            return new CalendarState(ulong.Parse(root.GetAttribute("ticks"),CultureInfo.InvariantCulture),double.Parse(root.GetAttribute("fraction"),CultureInfo.InvariantCulture));
        }
        internal void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary=path+".new";
            byte[] bytes=Encoding.UTF8.GetBytes(Serialize());
            using(var file=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough))
            { file.Write(bytes,0,bytes.Length); file.Flush(true); }
            if(File.Exists(path)) File.Replace(temporary,path,path+".bak");
            else File.Move(temporary,path);
        }
    }
}
