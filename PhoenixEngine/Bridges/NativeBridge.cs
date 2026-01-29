using PhoenixEngine.EngineManagement;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManage;
using PhoenixEngine.TranslateManagement;

namespace PhoenixEngine.Bridges
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
                return Phoenix.Version;
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

            public static string GetTranslatorCache(string Key)
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
                int FileUniqueKey = Phoenix.GetFileUniqueKey();

                QueryTransItem NQueryTransItem = new QueryTransItem();

                string TransText = "";

                string GetRamSource = "";
                if (Translator.TransData.ContainsKey(Key))
                {
                    GetRamSource = Translator.TransData[Key];
                }

                if (GetRamSource.Trim().Length == 0)
                {
                    TransText = LocalDBCache.GetCacheText(FileUniqueKey, Key, Phoenix.To);

                    if (TransText.Trim().Length > 0)
                    {
                        NQueryTransItem.FromCloud = false;
                    }
                    else
                    {
                        TransText = CloudDBCache.FindCache(FileUniqueKey, Key, Phoenix.To);

                        if (TransText.Trim().Length > 0)
                        {
                            NQueryTransItem.FromCloud = true;
                        }
                    }

                   
                    NQueryTransItem.State = 1;
                }
                else
                {
                    var GetStr = CloudDBCache.FindCache(FileUniqueKey, Key, Phoenix.To);
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
                int FileUniqueKey = Phoenix.GetFileUniqueKey();

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

                    CloudDBCache.DeleteCache(FileUniqueKey, Key, Phoenix.To);
                    LocalDBCache.DeleteCache(FileUniqueKey, Key, Phoenix.To);

                    return true;
                }

                var GetState = LocalDBCache.UPDateLocalTransItem(FileUniqueKey, Key, (int)Phoenix.To, SourceText,TransText, 0);

                Phoenix.GetTranslatedCount(Phoenix.GetFileUniqueKey());

                return GetState;
            }

            public static bool SetCloudTransData(string Key, string SourceText, string TransText)
            {
                int FileUniqueKey = Phoenix.GetFileUniqueKey();

                if (TransText.Trim().Length <= 0)
                {
                    if (Translator.TransData.ContainsKey(Key))
                    {
                        Translator.TransData.Remove(Key);
                    }

                    CloudDBCache.DeleteCache(FileUniqueKey, Key, Phoenix.To);
                    LocalDBCache.DeleteCache(FileUniqueKey, Key, Phoenix.To);

                    return true;
                }

                var GetState = CloudDBCache.AddCache(FileUniqueKey, Key, (int)Phoenix.To,SourceText, TransText);

                Phoenix.GetTranslatedCount(Phoenix.GetFileUniqueKey());

                return GetState;
            }
        }
    }
}
