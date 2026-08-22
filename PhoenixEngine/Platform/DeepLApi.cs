using System.Collections.Generic;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using PhoenixEngine.Language;
using PhoenixEngine.P_Delegate;
using PhoenixEngine.Request;
using PhoenixEngine.Translate;
using PhoenixEngine.Unit;

namespace PhoenixEngine.Platform
{
    public class DeepLItem
    {
        public List<string> text { get; set; }
        public string target_lang { get; set; } = "";
        public string tag_handling { get; set; } = "html";
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

    public class DeepLApi: I_TranslationNode
    {
        private static readonly HttpHelper HttpTransport = new HttpHelper();

        public static PlatformType Type = PlatformType.DeepL;
        public EngineConfigJson ConfigRef { get; set; } = null;
        public WebProxy ProxyRef { get; set; } = null;

        public int CustomID { get; set; } = 0;
        public void Init(int CustomID, EngineConfigJson Config, WebProxy Proxy)
        {
            this.CustomID = CustomID;
            this.ConfigRef = Config;
            this.ProxyRef = Proxy;
        }

        private static string DeepLFreeHost = "https://api-free.deepl.com/v2/translate";
        private static string DeepLHost = "https://api.deepl.com/v2/translate";
       
        public string QuickTrans(string ApiKey,UnitGroup Source, Languages FromLang, Languages ToLang,ref PlatformCall Call)
        {
            try
            {
                DeepLItem NDeepLItem = new DeepLItem();
                NDeepLItem.target_lang = P_Language.ToLanguageCode(ToLang).ToUpper();

                bool CanTrans = false;
                string TransSource = Source.GenContent(ref CanTrans);
                if (!CanTrans)
                {
                    return "<empty>";
                }

                NDeepLItem.text = new List<string>() { TransSource };

                string Send = JsonConvert.SerializeObject(NDeepLItem);
                string Recv = "";

                var GetResult = CallPlatform(ApiKey,NDeepLItem, ref Recv);

                Call = new PlatformCall(PlatformType.DeepL, FromLang,ToLang,Send,Recv,0);

                if (GetResult == null)
                {
                    return string.Empty;
                }
                if (GetResult.translations != null)
                {
                    if (GetResult.translations.Length > 0)
                    {
                        Call.Success = true;
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
        public DeepLResult CallPlatform(string ApiKey,DeepLItem Item,ref string Recv)
        {
            string GetJson = JsonConvert.SerializeObject(Item);
            WebHeaderCollection Headers = new WebHeaderCollection();
            Headers.Add("Authorization", string.Format("DeepL-Auth-Key {0}", ApiKey));
            string AutoHost = "";

            if (ConfigRef.GetPlatformData(DeepLApi.Type).IsFree)
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
                ContentType = "application/json; charset=utf-8",
                Encoding = Encoding.UTF8,
                WebProxy = ProxyRef
            };
            try
            {
                Http.Header.Add("Accept-Encoding", " gzip");
            }
            catch { }

            string GetResult = HttpTransport.GetHtml(Http).Html;
            Recv = GetResult;
            try
            {
                return JsonConvert.DeserializeObject<DeepLResult>(GetResult);
            }
            catch
            {
                return null;
            }
        }
    }
}
