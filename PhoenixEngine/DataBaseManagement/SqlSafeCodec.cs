using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhoenixEngine.DataBaseManagement
{
    public class SqlSafeCodec
    {
        private static readonly Dictionary<string, string> EncodeMap = new Dictionary<string, string>
        {
        { "'",  "[SQ]" },
        { "\"", "[DQ]" },
        { ";",  "[SM]" },
        { "--", "[CM]" },
        { "#",  "[SH]" },
        { "/*", "[CO]" },
        { "*/", "[CC]" },
        { "\\", "[BS]" },
        { "%",  "[PW]" },
        { "_",  "[US]" },
        { "=",  "[EQ]" },
        { "<",  "[LT]" },
        { ">",  "[GT]" },
        { "!",  "[NT]" },
        { "|",  "[PI]" },
        { "&",  "[AM]" },
        { "(",  "[LP]" },
        { ")",  "[RP]" },
        { "[",  "[LB]" },
        { "]",  "[RB]" },
        { "\0", "[N0]" },
        { "\r", "[CR]" },
        { "\n", "[LF]" }
        };

        private static readonly Dictionary<string, string> DecodeMap = EncodeMap
            .ToDictionary(kv => kv.Value, kv => kv.Key);

        public static string Encode(string Input)
        {
            if (Input == string.Empty) return string.Empty;
            string Result = Input;
            foreach (var kv in EncodeMap.OrderByDescending(x => x.Key.Length))
                Result = Result.Replace(kv.Key, kv.Value);
            return Result;
        }

        public static string Decode(string Input)
        {
            if (Input == string.Empty) return string.Empty;
            string Result = Input;
            foreach (var kv in DecodeMap)
                Result = Result.Replace(kv.Key, kv.Value);
            return Result;
        }
    }
}
