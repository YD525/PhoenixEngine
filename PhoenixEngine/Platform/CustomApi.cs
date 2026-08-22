using System.Net;
using System.Text;
using Newtonsoft.Json;
using PhoenixEngine.Common;
using PhoenixEngine.Language;
using PhoenixEngine.P_Delegate;
using PhoenixEngine.Request;
using PhoenixEngine.Translate;
using PhoenixEngine.Unit;

namespace PhoenixEngine.Platform
{
    public class CustomApi : I_TranslationNode
    {
        private static readonly HttpHelper HttpTransport = new HttpHelper();

        public static PlatformType Type = PlatformType.CustomPlatform;

        public CustomPlatformType CustomType = CustomPlatformType.Traditional;

        public CustomReqCore Core = new CustomReqCore();
        public EngineConfigJson ConfigRef { get; set; } = null;
        public WebProxy ProxyRef { get; set; } = null;
        public int CustomID { get; set; } = 0;
        public void Init(int CustomID,EngineConfigJson Config, WebProxy Proxy)
        { 
            this.CustomID = CustomID;
            this.ConfigRef = Config;
            this.ProxyRef = Proxy;
        }
        public string QuickTrans(string ApiKey, UnitGroup Source, Languages FromLang, Languages ToLang, ref PlatformCall Call)
        {
            var InFo = Phoenix.Config.GetPlatformData(CustomID).CustomInFo;

            bool CanTrans = false;
            string TransSource = Source.GenContent(ref CanTrans);
            if (!CanTrans)
            {
                return "<empty>";
            }

            Core.SetApiKey(ApiKey);

            Core.SetFrom(FromLang);
            Core.SetTo(ToLang);

            Core.SetUrl(InFo.Url);
            Core.SetHeader(InFo.Header);
            Core.SetPayLoad(InFo.PayLoad, InFo.PayLoadEncode);

            Core.SetSource(TransSource);

            Core.SetQueryRule(InFo.QueryRule);

            Core.SetSignMode(InFo.Sign);
            Core.SetSignParams(InFo.SignParams);

            string Send = "";
            string Recv = "";

            string Url = Core.GenUrl(InFo.Url_Tags);
            string PayLoad = Core.GenPayLoad(InFo.PayLoad_Tags);

            Send = "[Url]\n" + Url + "[Header]\n" + JsonConvert.SerializeObject(InFo.Header_Tags) + "[PayLoad]\n" + PayLoad;

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
                WebProxy = ProxyRef
            };

            string Result = HttpTransport.GetHtml(Http).Html;

            Recv = Result;

            if (Result.Trim().Length > 0)
            {
                string TransStr = "";
                if (Core.QueryRule.ByJson)
                {
                    var GetTags = CustomPlatformHelper.GetJsonValues(Result);

                    for (int i = 0; i < GetTags.Count; i++)
                    {
                        if (GetTags[i].Key.Equals(Core.QueryRule.FieldName))
                        {
                            TransStr = GetTags[i].Value;
                            break;
                        }
                    }
                }
                else
                if (Core.QueryRule.SplitStr.Trim().Length > 0)
                {
                    TransStr = Result.Substring(Result.LastIndexOf(Core.QueryRule.SplitStr) + Core.QueryRule.SplitStr.Length);
                }
                else
                if (Core.QueryRule.LeftStr.Trim().Length > 0)
                {
                    TransStr = Result.StringDivision(Core.QueryRule.LeftStr, Core.QueryRule.RightStr);
                }
                else
                {
                    TransStr = string.Empty;
                }

                if (TransStr.Trim().Length > 0)
                {
                    Call.Success = true;

                    return TransStr;
                }
                else
                {
                    return string.Empty;
                }
            }

            Call = new PlatformCall(PlatformType.DeepL, FromLang, ToLang, Send, Recv, 0);

            return "";
        }
    }
}
