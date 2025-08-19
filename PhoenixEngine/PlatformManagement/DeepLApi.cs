using System.Net;
using System.Text.Json;
using PhoenixEngine.DelegateManagement;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.RequestManagement;
using PhoenixEngine.TranslateCore;

namespace PhoenixEngine.PlatformManagement
{
    public class DeepLItem
    {
        public List<string> text { get; set; }
        public string target_lang { get; set; } = "";
    }

    public class DeepLResult
    {
        public DeepLTranslation[] translations { get; set; }
    }

    public class DeepLTranslation
    {
        public string detected_source_language { get; set; }
        public string text { get; set; }
    }



    public class DeepLApi
    {
        private static string DeepLFreeHost = "https://api-free.deepl.com/v2/translate";
        private static string DeepLHost = "https://api.deepl.com/v2/translate";
       
        public string QuickTrans(string TransSource, Languages FromLang, Languages ToLang)
        {
            try
            {
                DeepLItem NDeepLItem = new DeepLItem();
                NDeepLItem.target_lang = LanguageHelper.ToLanguageCode(ToLang).ToUpper();
                NDeepLItem.text = new List<string>() { TransSource };
                var GetResult = CallAI(NDeepLItem);
                if (GetResult == null)
                {
                    return string.Empty;
                }
                if (GetResult.translations != null)
                {
                    if (GetResult.translations.Length > 0)
                    {
                        return GetResult.translations[0].text;
                    }
                }

                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
        public DeepLResult? CallAI(DeepLItem Item)
        {
            string GetJson = JsonSerializer.Serialize(Item);
            WebHeaderCollection Headers = new WebHeaderCollection();
            Headers.Add("Authorization", string.Format("DeepL-Auth-Key {0}", EngineConfig.DeepLKey));
            string AutoHost = "";

            if (EngineConfig.IsFreeDeepL)
            {
                AutoHost = DeepLFreeHost;
            }
            else
            {
                AutoHost = DeepLHost;
            }

            HttpItem Http = new HttpItem()
            {
                URL = AutoHost,
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/132.0.0.0 Safari/537.36",
                Method = "Post",
                Header = Headers,
                Accept = "*/*",
                Postdata = GetJson,
                Cookie = "",
                ContentType = "application/json",
                Timeout = EngineConfig.GlobalRequestTimeOut,
                WebProxy = ProxyCenter.CurrentProxy
            };
            try
            {
                Http.Header.Add("Accept-Encoding", " gzip");
            }
            catch { }

            string GetResult = new HttpHelper().GetHtml(Http).Html;
            try
            {
                if (DelegateHelper.SetLog != null)
                {
                    DelegateHelper.SetLog("DeepL:" + GetResult, 1);
                }
                return JsonSerializer.Deserialize<DeepLResult>(GetResult);
            }
            catch
            {
                return null;
            }
        }
    }
}
