using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.RequestManagement;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManage;
using static PhoenixEngine.EngineManagement.DataTransmission;
using static PhoenixEngine.PlatformManagement.ChatGptApi;

namespace PhoenixEngine.PlatformManagement
{
    public class CustomAIApi : I_AI_TranslationNode
    {
        public static PlatformType Type = PlatformType.CustomPlatform;

        public CustomPlatformType CustomType = CustomPlatformType.CloudAI;

        public CustomReqCore Core = null;
        public string ApiKey { get; set; } = "";
        public string Model { get; set; } = "";
        public AITranslationMemory AIMemoryRef { get; set; } = null;
        public EngineConfigJson ConfigRef { get; set; } = null;
        public WebProxy ProxyRef { get; set; } = null;
        public int CustomID { get; set; } = 0;
        public void Init(int CustomID, AITranslationMemory AIMemory, EngineConfigJson Config, WebProxy Proxy)
        { 
            this.CustomID = CustomID;
            this.AIMemoryRef = AIMemory;
            this.ConfigRef = Config;

            this.ProxyRef = Proxy;

            Core = new CustomReqCore();

            CustomPlatformInFo QueryInFo = Phoenix.Config.GetPlatformData(CustomID).CustomInFo;

            Core.SetUrl(QueryInFo.Url);
            Core.SetHeader(QueryInFo.Header);
            Core.SetPayLoad(QueryInFo.PayLoad);
        }
        public void SetApiKey(string Key)
        { 
           this.ApiKey = Key;
        }

        public string QuickTrans(List<ReplaceTag> CustomWords, string TransSource, Languages FromLang, Languages ToLang, bool UseAIMemory, int AIMemoryCountLimit, string AIParam, ref AICall Call, string Type)
        {
            return string.Empty;
        }

        public void CallAI()
        {
            CustomPlatformInFo QueryInFo = Phoenix.Config.GetPlatformData(CustomID).CustomInFo;

            string Url = Core.GenUrl(QueryInFo.Url_Tags);
            string PayLoad = Core.GenPayLoad(QueryInFo.PayLoad_Tags);
            WebHeaderCollection Headers = Core.GenHeader(QueryInFo.Header_Tags);

            string Method = "Get";

            if (QueryInFo.IsPost)
            {
                Method = "Post";
            }

            HttpItem Http = new HttpItem()
            {
                URL = Url,
                UserAgent = Core.UserAgent,
                Method = Method,
                Header = Headers,
                Accept = Core.Accept,
                Postdata = PayLoad,
                Cookie = "",
                ContentType = Core.ContentType,
                Encoding = Encoding.UTF8,
                Timeout = ConfigRef.GlobalRequestTimeOut,
                WebProxy = ProxyRef
            };

            string GetResult = new HttpHelper().GetHtml(Http).Html;
        }
    }
}
