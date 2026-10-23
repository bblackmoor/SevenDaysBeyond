using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SevenDaysBeyond;

class LocalizedTextTests
{
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); }
    static byte[] Encode(LocalizedText message)
    { using (var s = new MemoryStream()) { using (var w = new BinaryWriter(s)) message.Write(w); return s.ToArray(); } }
    static LocalizedText Decode(byte[] bytes)
    {
        using (var s = new MemoryStream(bytes)) using (var r = new BinaryReader(s))
        { var m = LocalizedText.Read(r); Check(s.Position == s.Length, "complete packet consumed"); return m; }
    }
    static void Reject(Action action, string name)
    {
        try { action(); } catch (InvalidDataException) { return; }
        catch (EndOfStreamException) { return; }
        catch (System.Text.DecoderFallbackException) { return; }
        throw new Exception("Accepted " + name);
    }
    static void Main()
    {
        var en = new Dictionary<string,string> { {"sdb_report", "Walk {0} to {1}: {2}. {1}"}, {"sdb_direction", "east"} };
        var es = new Dictionary<string,string> { {"sdb_report", "Hacia {1}: camina al {0}; {2}. {1}"}, {"sdb_direction", "este"} };
        Func<string,string> english = k => en.ContainsKey(k) ? en[k] : k;
        var source = LocalizedText.Message("sdb_report", LocalizedText.Key("sdb_direction"), "NorthEast", "{0}, á 日本語");
        var copy = Decode(Encode(source));
        CultureInfo.CurrentCulture = new CultureInfo("es-ES");
        Check(copy.Render(k => es[k], english) == "Hacia NorthEast: camina al este; {0}, á 日本語. NorthEast", "per-client language, reordered/repeated placeholders and literal values");
        Check(copy.Render(english, english) == "Walk east to NorthEast: {0}, á 日本語. NorthEast", "same packet in English");
        Check(copy.Render(k => k, english) == copy.Render(english, english), "missing translations fall back");
        Check(copy.Render(k => "", english) == copy.Render(english, english), "empty translations fall back");
        Check(copy.Render(k => k == "sdb_report" ? "bad {99}" : "east", english) == copy.Render(english, english), "bad format falls back");
        var bytes = Encode(source); bytes[0] = 99;
        Reject(() => Decode(bytes), "unknown protocol");
        Reject(() => Decode(new byte[]{1,2}), "invalid node type");
        Reject(() => Decode(new byte[]{1,1,0xff,0xff}), "oversized length before allocation");
        Reject(() => Decode(new byte[]{1,1,3,0,65}), "truncated string");
        Reject(() => Decode(new byte[]{1,1,1,0,0xff}), "invalid UTF-8");
        Reject(() => Encode(LocalizedText.Key("other_mod_key")), "foreign key");
        Reject(() => Encode(LocalizedText.Message("sdb_report", new string('x',4097))), "oversized argument");
        Reject(() => Encode(LocalizedText.Key("sdb_report", new LocalizedText[17])), "excess arguments");
        var deep = LocalizedText.Key("sdb_direction");
        for (int i=0;i<6;i++) deep = LocalizedText.Key("sdb_report", deep);
        Reject(() => Encode(deep), "excess depth");
        var many = new LocalizedText[16];
        for(int i=0;i<16;i++) many[i]=LocalizedText.Message("sdb_report",1,2,3,4);
        Reject(() => Encode(LocalizedText.Key("sdb_report",many)), "excess total nodes");
        Console.WriteLine("PASS localized wire roundtrip, independent languages, fallback, UTF-8, placeholders and malformed-packet bounds");
    }
}
