using PhoenixEngine.ADO;

namespace PhoenixEngine.Translate
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
                var Link = Translator.GetLink();

                Link[Key] = Value;
            }
        }
        public static string GetLink(this Translator Translator, string Key)
        {
            lock (Translator.TransDataLocker)
            {
                var Link = Translator.GetLink();

                var Result = Link[Key];

                if (Result != null)
                {
                    return Result;
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
                var Link = Translator.GetLink();
                var GetResult = Link[Key];
                if (GetResult != null)
                {
                    return GetResult;
                }
                else
                {
                    Link.Add(Key, string.Empty);
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
            int FileUniqueKey = Translator.GetFileUniqueKey();

            QueryTransItem NQueryTransItem = new QueryTransItem();

            string TransText = "";

            string GetRamSource = Translator.GetLink(Key);

            if (GetRamSource.Trim().Length == 0)
            {
                TransText = LocalDBCache.GetCacheText(FileUniqueKey, Key, Translator.To);

                if (TransText.Trim().Length > 0)
                {
                    NQueryTransItem.FromCloud = false;
                }
                else
                {
                    TransText = CloudDBCache.FindCache(FileUniqueKey, Key, Translator.To);

                    if (TransText.Trim().Length > 0)
                    {
                        NQueryTransItem.FromCloud = true;
                    }
                }


                NQueryTransItem.State = 1;
            }
            else
            {
                var GetStr = CloudDBCache.FindCache(FileUniqueKey, Key, Translator.To);
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
            int FileUniqueKey = Translator.GetFileUniqueKey();

            var Link = Translator.GetLink();

            if (TransText.Trim().Length > 0)
            {
                Link[Key] = TransText;
            }
            else
            {
                Link.Remove(Key);

                CloudDBCache.DeleteCache(FileUniqueKey, Key, Translator.To);
                LocalDBCache.DeleteCache(FileUniqueKey, Key, Translator.To);

                return true;
            }

            var GetState = LocalDBCache.UPDateLocalTransItem(FileUniqueKey, Key, (int)Translator.To, SourceText, TransText, 0);

            return GetState;
        }
        public static bool SetCloudData(this Translator Translator,int FileUniqueKey, string Key, string SourceText, string TransText)
        {
            if (TransText.Trim().Length <= 0)
            {
                var Link = Translator.GetLink();

                Link.Remove(Key);

                CloudDBCache.DeleteCache(FileUniqueKey, Key, Translator.To);
                LocalDBCache.DeleteCache(FileUniqueKey, Key, Translator.To);

                return true;
            }

            var GetState = CloudDBCache.AddCache(FileUniqueKey, Key, (int)Translator.To, SourceText, TransText);

            Translator.GetTranslatedCount();

            return GetState;
        }
    }
}
