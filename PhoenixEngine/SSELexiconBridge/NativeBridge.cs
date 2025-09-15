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
                return Engine.Version;
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
                int FileUniqueKey = Engine.GetFileUniqueKey();

                QueryTransItem NQueryTransItem = new QueryTransItem();

                string TransText = "";

                string GetRamSource = "";
                if (Translator.TransData.ContainsKey(Key))
                {
                    GetRamSource = Translator.TransData[Key];
                }

                if (GetRamSource.Trim().Length == 0)
                {
                    TransText = LocalDBCache.GetCacheText(FileUniqueKey, Key, Engine.To);

                    if (TransText.Trim().Length > 0)
                    {
                        NQueryTransItem.FromCloud = false;
                    }
                    else
                    {
                        TransText = CloudDBCache.FindCache(FileUniqueKey, Key, Engine.To);

                        if (TransText.Trim().Length > 0)
                        {
                            NQueryTransItem.FromCloud = true;
                        }
                    }

                   
                    NQueryTransItem.State = 1;
                }
                else
                {
                    var GetStr = CloudDBCache.FindCache(FileUniqueKey, Key, Engine.To);
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
                int FileUniqueKey = Engine.GetFileUniqueKey();

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

                    CloudDBCache.DeleteCache(FileUniqueKey, Key, Engine.To);
                    LocalDBCache.DeleteCache(FileUniqueKey, Key, Engine.To);

                    return true;
                }

                var GetState = LocalDBCache.UPDateLocalTransItem(FileUniqueKey, Key, (int)Engine.To, TransText, 0);

                Engine.GetTranslatedCount(Engine.GetFileUniqueKey());

                return GetState;
            }

            public static bool SetCloudTransData(string Key, string SourceText, string TransText)
            {
                int FileUniqueKey = Engine.GetFileUniqueKey();

                if (TransText.Trim().Length <= 0)
                {
                    if (Translator.TransData.ContainsKey(Key))
                    {
                        Translator.TransData.Remove(Key);
                    }

                    CloudDBCache.DeleteCache(FileUniqueKey, Key, Engine.To);
                    LocalDBCache.DeleteCache(FileUniqueKey, Key, Engine.To);

                    return true;
                }

                var GetState = CloudDBCache.AddCache(FileUniqueKey, Key, (int)Engine.To, TransText);

                Engine.GetTranslatedCount(Engine.GetFileUniqueKey());

                return GetState;
            }
        }
    }
}
