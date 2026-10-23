using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace SevenDaysBeyond
{
    // A message tree contains translation keys and literal values, never executable markup.
    // This codec is independent of Unity so the actual wire format can be tested offline.
    internal sealed class LocalizedText
    {
        internal const byte Protocol = 1;
        internal const int MaxDepth = 4, MaxNodes = 64, MaxArguments = 16, MaxStringBytes = 4096;
        internal readonly string Value;
        internal readonly LocalizedText[] Arguments;
        internal bool IsLiteral { get { return Arguments == null; } }
        private LocalizedText(string value, LocalizedText[] arguments)
        { Value = value ?? ""; Arguments = arguments; }
        internal static LocalizedText Literal(object value)
        { return new LocalizedText(Convert.ToString(value, CultureInfo.InvariantCulture), null); }
        internal static LocalizedText Key(string key, params LocalizedText[] arguments)
        { return new LocalizedText(key, arguments ?? new LocalizedText[0]); }
        internal static LocalizedText Message(string key, params object[] arguments)
        { return Key(key, Array.ConvertAll(arguments, a => a as LocalizedText ?? Literal(a))); }

        internal void Write(BinaryWriter writer)
        {
            // Validate the complete tree before writing any bytes.
            int nodes = 0; Validate(0, ref nodes);
            writer.Write(Protocol); WriteNode(writer);
        }
        private void Validate(int depth, ref int nodes)
        {
            if (depth > MaxDepth || ++nodes > MaxNodes || Encoding.UTF8.GetByteCount(Value) > MaxStringBytes
                || (!IsLiteral && (!ValidKey(Value) || Arguments.Length > MaxArguments)))
                throw new InvalidDataException("Invalid localized message.");
            if (!IsLiteral) foreach (var arg in Arguments)
            { if (arg == null) throw new InvalidDataException("Null message argument."); arg.Validate(depth + 1, ref nodes); }
        }
        private static bool ValidKey(string key)
        {
            if (!key.StartsWith("sdb_", StringComparison.Ordinal) || key.Length > 96) return false;
            foreach (char c in key) if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '_') return false;
            return true;
        }
        private void WriteNode(BinaryWriter writer)
        {
            writer.Write(IsLiteral);
            byte[] bytes = Encoding.UTF8.GetBytes(Value);
            writer.Write((ushort)bytes.Length); writer.Write(bytes);
            if (IsLiteral) return;
            writer.Write((byte)Arguments.Length);
            foreach (var arg in Arguments) arg.WriteNode(writer);
        }
        internal static LocalizedText Read(BinaryReader reader)
        {
            if (reader.ReadByte() != Protocol) throw new InvalidDataException("Unsupported localized message protocol.");
            int nodes = 0; return ReadNode(reader, 0, ref nodes);
        }
        private static LocalizedText ReadNode(BinaryReader reader, int depth, ref int nodes)
        {
            if (depth > MaxDepth || ++nodes > MaxNodes) throw new InvalidDataException("Localized message too complex.");
            byte kind = reader.ReadByte();
            if (kind > 1) throw new InvalidDataException("Invalid message node type.");
            bool literal = kind == 1;
            int count = reader.ReadUInt16();
            if (count > MaxStringBytes) throw new InvalidDataException("Localized value too large.");
            byte[] bytes = reader.ReadBytes(count);
            if (bytes.Length != count) throw new EndOfStreamException();
            string value = new UTF8Encoding(false, true).GetString(bytes);
            if (literal) return Literal(value);
            if (!ValidKey(value)) throw new InvalidDataException("Invalid localization key.");
            count = reader.ReadByte();
            if (count > MaxArguments) throw new InvalidDataException("Too many localized arguments.");
            var arguments = new LocalizedText[count];
            for (int i = 0; i < count; i++) arguments[i] = ReadNode(reader, depth + 1, ref nodes);
            return Key(value, arguments);
        }
        internal string Render(Func<string, string> lookup, Func<string, string> english)
        {
            if (IsLiteral) return Value;
            object[] args = Array.ConvertAll(Arguments, a => (object)a.Render(lookup, english));
            string template = lookup(Value);
            if (string.IsNullOrEmpty(template) || template == Value || template == "UL_" + Value) template = english(Value);
            try { return string.Format(CultureInfo.CurrentCulture, template, args); }
            catch (FormatException) { return string.Format(CultureInfo.InvariantCulture, english(Value), args); }
        }
    }
}
