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
        public class TranslatorBridge
        {
            public static void FormatData()
            {
                lock (Translator.TransDataLocker)
                {
                    Translator.FormatData();
                } 
            }

            public static void ClearCache()
            {
                lock (Translator.TransDataLocker)
                {
                    Translator.ClearCache();
                }
            }

            public static string? GetTranslatorCache(string Key)
            {
                lock (Translator.TransDataLocker)
                {
                    if (Translator.TransData.ContainsKey(Key))
                    {
                        return Translator.TransData[Key];
                    }
                    else
                    {
                        return null;
                    }
                }
            }

            public static string GetTransData(string Key)
            {
                lock (Translator.TransDataLocker)
                {
                    var GetResult = GetTranslatorCache(Key);
                    if (GetResult != null)
                    {
                        return GetResult;
                    }
                    else
                    {
                        Translator.TransData.Add(Key, string.Empty);
                    }
                    return string.Empty;
                }  
            }

            public static void SetTransData(string Key, string Value)
            {
                lock (Translator.TransDataLocker)
                {
                    if (Translator.TransData.ContainsKey(Key))
                    {
                        Translator.TransData[Key] = Value;
                    }
                    else
                    {
                        Translator.TransData.Add(Key, Value);
                    }
                }
            }
        }
    }
}
