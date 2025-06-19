using System;
using System.Collections.Generic;
using System.Data.Entity.Core.Metadata.Edm;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using PhoenixEngine.Engine;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManage;
using PhoenixEngine.TranslateManagement;
using static System.Net.Mime.MediaTypeNames;
using static PhoenixEngine.SSEATComBridge.InteropDispatcher;

namespace PhoenixEngine.SSEATComBridge
{
    
    /// <summary>
    /// For SSEAT
    /// </summary>
    [ComVisible(true)]
    public class InteropDispatcher
    {
        public static void FormatData()
        {
            Translator.FormatData();
        }

        public static void ClearCache()
        {
            Translator.ClearCache();
        }

        public static string? GetTranslatorCache(string Key)
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

        public static string GetTransData(string Key)
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

        public static void SetTransData(string Key, string Value)
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

        public static int ConvertLangToID(string Lang)
        { 
            Languages GetLang = (Languages)Enum.Parse(typeof(Languages), Lang);
            return (int)GetLang;
        }

        public static string translate(string Key,string Text,int Src,int Dst)
        {
            try 
            {
                if (Key.Trim().Length == 0 || Key == null)
                {
                    throw new Exception("Key is Null!");
                }

                bool CanSleep = true;
                return Translator.QuickTrans(EngineConfig.CurrentModName,string.Empty,Key,Text,(Languages)Src, (Languages)Dst,ref CanSleep);
            }
            catch (Exception e) 
            {
                throw;
            }
        }
    }
}
