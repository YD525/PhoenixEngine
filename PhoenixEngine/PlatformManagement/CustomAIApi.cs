using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PhoenixEngine.ConvertManager;
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

        private CustomPlatformInFo InFo = null;
        public void Init(int CustomID, AITranslationMemory AIMemory, EngineConfigJson Config, WebProxy Proxy)
        { 
            this.CustomID = CustomID;
            this.AIMemoryRef = AIMemory;
            this.ConfigRef = Config;

            this.ProxyRef = Proxy;

            Core = new CustomReqCore();

            InFo = Phoenix.Config.GetPlatformData(CustomID).CustomInFo;

            Core.SetUrl(InFo.Url);
            Core.SetHeader(InFo.Header);
            Core.SetPayLoad(InFo.PayLoad,InFo.PayLoadEncode);

            Core.SetQueryRule(InFo.QueryRule);
        }
        public void SetApiKey(string Key)
        { 
            this.ApiKey = Key;
            this.Core.SetApiKey(this.ApiKey);
        }

        public string QuickTrans(List<ReplaceTag> CustomWords, string TransSource, Languages FromLang, Languages ToLang, bool UseAIMemory, int AIMemoryCountLimit, string AIParam, ref AICall Call, string Type)
        {
            return CallAI();
        }

        public string CallAI()
        {
            string Url = Core.GenUrl(InFo.Url_Tags);
            string PayLoad = Core.GenPayLoad(InFo.PayLoad_Tags);
            WebHeaderCollection Headers = Core.GenHeader(InFo.Header_Tags);

            string Method = "Get";

            if (InFo.IsPost)
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

            if (GetResult.Trim().Length > 0)
            {
                if (Core.QueryRule.ByJson)
                {
                    var GetTags = CustomPlatformHelper.GetJsonValues(GetResult);

                    for (int i = 0; i < GetTags.Count; i++)
                    {
                        if (GetTags[i].Key.Equals(Core.QueryRule.FieldName))
                        {
                            return GetTags[i].Value;
                        }
                    }
                }
                else
                if (Core.QueryRule.SplitStr.Trim().Length > 0)
                {
                    return GetResult.Split(Core.QueryRule.SplitStr[0])[1];
                }
                else
                if (Core.QueryRule.LeftStr.Trim().Length > 0)
                {
                    return ConvertHelper.StringDivision(GetResult, Core.QueryRule.LeftStr, Core.QueryRule.RightStr);
                }
                else
                {
                    return GetResult;
                }
            }

            return string.Empty;
        }
    }
}
