using System;
using System.Collections.Generic;
using System.Data.Entity.Core.Metadata.Edm;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PhoenixEngine.TranslateManage;

namespace PhoenixEngine.SSELexiconBridge
{
    /// <summary>
    /// For SSE Lexicon
    /// </summary>
    public class NativeBridge
    {
        public static void FormatData()
        {
            Translator.FormatData();
        }


        public static string GetTransData(string Key)
        {
            if (Translator.TransData.ContainsKey(Key))
            {
                return Translator.TransData[Key];
            }
            else
            {
                Translator.TransData.Add(Key, string.Empty);
            }
            return string.Empty;
        }
    }
}
