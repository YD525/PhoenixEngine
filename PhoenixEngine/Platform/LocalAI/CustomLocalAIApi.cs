using System.Collections.Generic;
using System.Net;
using System.Text;
using PhoenixEngine.Common;
using PhoenixEngine.Engine;
using PhoenixEngine.Language;
using PhoenixEngine.Memory;
using PhoenixEngine.P_Delegate;
using PhoenixEngine.Request;
using PhoenixEngine.Translate;
using PhoenixEngine.Unit;

namespace PhoenixEngine.Platform.LocalAI
{
    public class CustomLocalAIApi : I_Local_AI_TranslationNode
    {
        private static readonly HttpHelper HttpTransport = new HttpHelper();

        public static PlatformType Type = PlatformType.CustomPlatform;

        public CustomPlatformType CustomType = CustomPlatformType.LocalAI;

        public string Model { get; set; } = "";

        public AITranslationMemory AIMemoryRef { get; set; } = null;
        public EngineConfigJson ConfigRef { get; set; } = null;

        public CustomReqCore Core = new CustomReqCore();
        public int LocalPort { get; set; } = 0;
        public int CustomID { get; set; } = 0;
        public void Init(int CustomID, AITranslationMemory AIMemory, EngineConfigJson Config)
        { 
            this.CustomID = CustomID;
            this.AIMemoryRef = AIMemory;
            this.ConfigRef = Config;
        }

        public string QuickTrans(List<ReplaceTag> CustomWords, UnitGroup Source, Languages FromLang, Languages ToLang, bool UseAIMemory, int AIMemoryCountLimit, string AIParam, ref AICall Call)
        {
            var InFo = Phoenix.Config.GetPlatformData(CustomID).CustomInFo;

            Core.SetQueryRule(InFo.QueryRule);

            List<string> Related = new List<string>();

            if (ConfigRef.ContextEnable && UseAIMemory)
            {
                Related = Source.QueryAIMemory(FromLang,ToLang,AIMemoryCountLimit);
            }

            bool CanTrans = false;
            string TransSource = Source.GenContent(ref CanTrans);
            if (!CanTrans)
            {
                return "<empty>";
            }

            if (ConfigRef.UserCustomAIPrompt.Trim().Length > 0)
            {
                AIParam = AIParam + "\n" + ConfigRef.UserCustomAIPrompt;
            }

            var GetTransSource = AIPrompt.GenerateTranslationPrompt(FromLang, ToLang, TransSource,Related, CustomWords, AIParam);

            string Send = GetTransSource;
            string Recv = "";

            var Result = CallAI(Send, ref Recv);

            Call = new AICall(PlatformType.CustomPlatform, Send, Recv, this.CustomID);

            if (Result != null)
            {
                if (Result.Length > 0)
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
                        TransStr = Result;
                    }

                    string GetStr = "";

                    if (TransStr.Trim().Length > 0)
                    {
                        Call.Success = true;

                        return GetStr;
                    }
                    else
                    {
                        return string.Empty;
                    }
                }
                else
                {
                    Call.Success = false;
                }
            }
            else
            {
                Call.Success = false;
            }
            return string.Empty;
        }

        public string CallAI(string Send, ref string Recv)
        {
            CustomReqCore Core = new CustomReqCore();

            var InFo = Phoenix.Config.GetPlatformData(CustomID).CustomInFo;

            Core.SetModel(Model);
            Core.SetUrl(InFo.Url);
            Core.SetHeader(InFo.Header);
            Core.SetPayLoad(InFo.PayLoad, InFo.PayLoadEncode);
            Core.SetQueryRule(InFo.QueryRule);
            Core.SetPrompt(Send);

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
                MaximumResponseBytes = JsonPayload.MaximumDocumentBytes
            };

            string GetResult = HttpTransport.GetHtml(Http).Html;

            Recv = GetResult;

            if (GetResult.Trim().Length > 0)
            {
                return GetResult;
            }

            return string.Empty;
        }
    }
}
