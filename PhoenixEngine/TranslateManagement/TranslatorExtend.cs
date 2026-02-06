using PhoenixEngine.EngineManagement;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManage;

namespace PhoenixEngine.TranslateManagement
{
    public static class TranslatorFunc
    {
        public class QueryTransItem
        {
            public string Key = "";
            public string TransText = "";
            public bool FromCloud = false;
            public int State = 0;
        }
        public static void SetLink(this Translator Translator, string Key, string Value)
        {
            lock (Translator.TransDataLocker)
            {
                if (Translator.TranslatedLink.ContainsKey(Key))
                {
                    Translator.TranslatedLink[Key] = Value;
                }
                else
                {
                    Translator.TranslatedLink.Add(Key, Value);
                }
            }
        }
        public static string GetLink(this Translator Translator, string Key)
        {
            lock (Translator.TransDataLocker)
            {
                if (Translator.TranslatedLink.ContainsKey(Key))
                {
                    return Translator.TranslatedLink[Key];
                }
                else
                {
                    return string.Empty;
                }
            }
        }
        public static string GetOrAddTranslatorCache(this Translator Translator, string Key)
        {
            lock (Translator.TransDataLocker)
            {
                var GetResult = Translator.GetLink(Key);
                if (GetResult != null)
                {
                    return GetResult;
                }
                else
                {
                    Translator.TranslatedLink.Add(Key, string.Empty);
                }
                return string.Empty;
            }
        }
        public static void ClearTranslationCache(this Translator Translator)
        {
            lock (Translator.TransDataLocker)
            {
                Translator.ClearCache();
            }
        }
        public static void UnifiedSymbols(this Translator Translator)
        {
            lock (Translator.TransDataLocker)
            {
                Translator.UnifiedSymbols();
            }
        }
        public static QueryTransItem QueryTransData(this Translator Translator,string Key)
        {
            int FileUniqueKey = Phoenix.GetFileUniqueKey();

            QueryTransItem NQueryTransItem = new QueryTransItem();

            string TransText = "";

            string GetRamSource = Translator.GetLink(Key);

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
        public static bool AutoSetLink(this Translator Translator, string Key, string SourceText, string TransText)
        {
            int FileUniqueKey = Phoenix.GetFileUniqueKey();

            if (TransText.Trim().Length > 0)
            {
                Translator.TranslatedLink[Key] = TransText;
            }
            else
            {
                if (Translator.TranslatedLink.ContainsKey(Key))
                {
                    Translator.TranslatedLink.Remove(Key);
                }

                CloudDBCache.DeleteCache(FileUniqueKey, Key, Phoenix.To);
                LocalDBCache.DeleteCache(FileUniqueKey, Key, Phoenix.To);

                return true;
            }

            var GetState = LocalDBCache.UPDateLocalTransItem(FileUniqueKey, Key, (int)Phoenix.To, SourceText, TransText, 0);

            Phoenix.GetTranslatedCount(Phoenix.GetFileUniqueKey());

            return GetState;
        }
        public static bool SetCloudData(this Translator Translator,int FileUniqueKey, string Key, string SourceText, string TransText)
        {
            if (TransText.Trim().Length <= 0)
            {
                if (Translator.TranslatedLink.ContainsKey(Key))
                {
                    Translator.TranslatedLink.Remove(Key);
                }

                CloudDBCache.DeleteCache(FileUniqueKey, Key, Phoenix.To);
                LocalDBCache.DeleteCache(FileUniqueKey, Key, Phoenix.To);

                return true;
            }

            var GetState = CloudDBCache.AddCache(FileUniqueKey, Key, (int)Phoenix.To, SourceText, TransText);

            Phoenix.GetTranslatedCount(Phoenix.GetFileUniqueKey());

            return GetState;
        }
    }
}
