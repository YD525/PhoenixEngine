using System.Collections.Generic;
using System.Text;

namespace PhoenixEngine.DataBaseManagement
{
    public static class SqlSafeCodec
    {
        private static readonly char[] DangerChars = new char[]
        {
        '\'', '\"', ';', '-', '#', '/', '\\', '%', '_', '=', '<', '>', '!',
        '|', '&', '(', ')', '[', ']', '\r', '\n', '\0'
        };

        private static readonly Dictionary<char, char> EncodeMap;
        private static readonly Dictionary<char, char> DecodeMap;
        private static readonly HashSet<char> EncodedSet;

        static SqlSafeCodec()
        {
            EncodeMap = new Dictionary<char, char>(DangerChars.Length);
            DecodeMap = new Dictionary<char, char>(DangerChars.Length);
            EncodedSet = new HashSet<char>();

            int baseCode = 0xE000;
            for (int i = 0; i < DangerChars.Length; i++)
            {
                char source = DangerChars[i];
                char mapped = (char)(baseCode + i);
                EncodeMap[source] = mapped;
                DecodeMap[mapped] = source;
                EncodedSet.Add(mapped);
            }
        }

        public static bool IsEncoded(string input)
        {
            if (string.IsNullOrEmpty(input)) return false;
            foreach (var c in input)
            {
                if (EncodedSet.Contains(c)) return true;
            }
            return false;
        }

        public static string Encode(string input)
        {
            if (input == null) return null;
            if (input.Length == 0) return string.Empty;

            if (IsEncoded(input)) return input;

            var sb = new StringBuilder(input.Length);
            foreach (var c in input)
            {
                if (EncodeMap.TryGetValue(c, out var m))
                    sb.Append(m);
                else
                    sb.Append(c);
            }
            return sb.ToString();
        }

        public static string Decode(string input)
        {
            if (input == null) return null;
            if (input.Length == 0) return string.Empty;

            var sb = new StringBuilder(input.Length);
            foreach (var c in input)
            {
                if (DecodeMap.TryGetValue(c, out var o))
                    sb.Append(o);
                else
                    sb.Append(c);
            }
            return sb.ToString();
        }

        public static string EncodeForSqlLiteral(string input)
        {
            var s = Encode(input) ?? string.Empty;
            return $"'{s}'";
        }
    }
}
