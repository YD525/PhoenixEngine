using System;
using System.Collections.Generic;
using System.Data.Entity.Core.Metadata.Edm;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManage;
using PhoenixEngine.TranslateManagement;

namespace PhoenixEngine.SSELexiconBridge
{
    /// <summary>
    /// For SSE Lexicon
    /// </summary>
    public class NativeBridge
    {
        public class TranslatorBridge
        {
            public static string GetVersion()
            {
                return DeFine.Version;
            }
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

            public static string GetTransCache(string Key)
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

            public static void SetTransCache(string Key, string Value)
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

            public class QueryTransItem
            {
                public string Key = "";
                public string TransText = "";
                public bool FromCloud = false;
                public int State = 0;
            }

            public static QueryTransItem QueryTransData(string Key, string SourceText)
            {
                string ModName = Engine.GetModName();

                QueryTransItem NQueryTransItem = new QueryTransItem();

                string TransText = "";

                string GetRamSource = "";
                if (Translator.TransData.ContainsKey(Key))
                {
                    GetRamSource = Translator.TransData[Key];
                }

                if (GetRamSource.Trim().Length == 0)
                {
                    TransText = LocalDBCache.GetCacheText(ModName, Key, Engine.To);

                    if (TransText.Trim().Length > 0)
                    {
                        NQueryTransItem.FromCloud = false;
                    }
                    else
                    {
                        TransText = CloudDBCache.FindCache(ModName, Key, Engine.To);

                        if (TransText.Trim().Length > 0)
                        {
                            NQueryTransItem.FromCloud = true;
                        }
                    }

                   
                    NQueryTransItem.State = 1;
                }
                else
                {
                    var GetStr = CloudDBCache.FindCache(ModName, Key, Engine.To);
                    TransText = GetRamSource;

                    if (GetStr.Equals(GetRamSource))
                    {
                        NQueryTransItem.FromCloud = true;
                    }
                    else
                    {
                        NQueryTransItem.FromCloud = false;
                    }

                    NQueryTransItem.State = 0;
                }


                NQueryTransItem.Key = Key;
                NQueryTransItem.TransText = TransText;
                return NQueryTransItem;
            }

            public static bool SetTransData(string Key, string SourceText,string TransText)
            {
                string ModName = Engine.GetModName();

                if (TransText.Trim().Length > 0)
                {
                    Translator.TransData[Key] = TransText;
                }
                else
                {
                    if (Translator.TransData.ContainsKey(Key))
                    {
                        Translator.TransData.Remove(Key);
                    }

                    CloudDBCache.DeleteCache(ModName, Key, Engine.To);
                    LocalDBCache.DeleteCache(ModName, Key, Engine.To);
                }

                var GetState = LocalDBCache.UPDateLocalTransItem(ModName, Key, (int)Engine.To, TransText, 0);

                Engine.GetTranslatedCount(Engine.GetModName());

                return GetState;
            }

            public static bool SetCloudTransData(string Key, string SourceText, string TransText)
            {
                string ModName = Engine.GetModName();

                if (TransText.Trim().Length <= 0)
                {
                    if (Translator.TransData.ContainsKey(Key))
                    {
                        Translator.TransData.Remove(Key);
                    }

                    CloudDBCache.DeleteCache(ModName, Key, Engine.To);
                    LocalDBCache.DeleteCache(ModName, Key, Engine.To);
                }

                var GetState = CloudDBCache.AddCache(ModName, Key, (int)Engine.To, TransText);

                Engine.GetTranslatedCount(Engine.GetModName());

                return GetState;
            }
        }
    }
}
