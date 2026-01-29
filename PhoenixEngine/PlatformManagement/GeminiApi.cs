using System.Collections.Generic;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.RequestManagement;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManage;
using static PhoenixEngine.EngineManagement.DataTransmission;

namespace PhoenixEngine.PlatformManagement
{

    public class GeminiItem
    {
        public List<GeminiContent> contents { get; set; } = new List<GeminiContent>();
    }

    public class GeminiContent
    {
        public List<GeminiPart> parts { get; set; } = new List<GeminiPart>();
    }

    public class GeminiPart
    {
        public string text { get; set; }
    }


    public class GeminiRootobject
    {
        public GeminiCandidate[] candidates { get; set; }
        public GeminiUsagemetadata usageMetadata { get; set; }
        public string modelVersion { get; set; }
    }

    public class GeminiUsagemetadata
    {
        public int promptTokenCount { get; set; }
        public int candidatesTokenCount { get; set; }
        public int totalTokenCount { get; set; }
        public GeminiPrompttokensdetail[] promptTokensDetails { get; set; }
        public GeminiCandidatestokensdetail[] candidatesTokensDetails { get; set; }
    }

    public class GeminiPrompttokensdetail
    {
        public string modality { get; set; }
        public int tokenCount { get; set; }
    }

    public class GeminiCandidatestokensdetail
    {
        public string modality { get; set; }
        public int tokenCount { get; set; }
    }

    public class GeminiCandidate
    {
        public GeminiContent content { get; set; }
        public string finishReason { get; set; }
        public float avgLogprobs { get; set; }
    }

    public class GeminiRContent
    {
        public GeminiRPart[] parts { get; set; }
        public string role { get; set; }
    }

    public class GeminiRPart
    {
        public string text { get; set; }
    }

    public class GeminiApi: I_AI_TranslationNode
    {
        public static PlatformType Type = PlatformType.Gemini;
        public string ApiKey { get; set; } = "";
        public string Model { get; set; } = "";
        public void SetApiKey(string Key)
        {
            this.ApiKey = Key;
        }
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
        }

        public string QuickTrans(List<ReplaceTag> CustomWords,string TransSource, Languages FromLang, Languages ToLang, bool UseAIMemory, int AIMemoryCountLimit, string AIParam, ref AICall Call,string Type)
        {
            List<string> Related = new List<string>();

            if (ConfigRef.ContextEnable && UseAIMemory)
            {
                Related = AIMemoryRef.FindRelevantTranslations(FromLang, ToLang, TransSource, AIMemoryCountLimit);
            }

            if (ConfigRef.UserCustomAIPrompt.Trim().Length > 0)
            {
                AIParam = AIParam + "\n" + ConfigRef.UserCustomAIPrompt;
            }

            var GetTransSource = AIPrompt.GenerateTranslationPrompt(FromLang, ToLang, TransSource, Type, Related, CustomWords, AIParam);

            string Send = GetTransSource;
            string Recv = "";
            var GetResult = CallAI(Send, ref Recv);

            Call = new AICall(PlatformType.Gemini, Send, Recv);

            if (GetResult != null)
            {
                try
                {
                    if (GetResult.candidates != null)
                    {
                        string GetStr = "";
                        if (GetResult.candidates.Length > 0)
                        {
                            if (GetResult.candidates[0].content.parts.Count > 0)
                            {
                                GetStr = GetResult.candidates[0].content.parts[0].text.Trim();
                            }
                        }
                        if (GetStr.Trim().Length > 0)
                        {
                            try
                            {
                                GetStr = JsonGeter.GetValue(GetStr);
                            }
                            catch
                            {
                                return string.Empty;
                            }

                            if (GetStr.Trim().Equals("<translated_text>"))
                            {
                                return string.Empty;
                            }

                            Call.Success = true;

                            return GetStr;
                        }
                        else
                        {
                            return string.Empty;
                        }
                    }
                }
                catch { return string.Empty; }
            }
            return string.Empty;
        }

        public GeminiRootobject CallAI(string Msg, ref string Recv)
        {
            int GetCount = Msg.Length;
            GeminiItem NGeminiItem = new GeminiItem();
            NGeminiItem.contents.Add(new GeminiContent());
            NGeminiItem.contents[0].parts.Add(new GeminiPart());
            NGeminiItem.contents[0].parts[0].text = Msg;
            var GetResult = CallAI(NGeminiItem,ref Recv);
            return GetResult;
        }

        public GeminiRootobject CallAI(GeminiItem Item, ref string Recv)
        {
            string GetJson = JsonConvert.SerializeObject(Item);
            WebHeaderCollection Headers = new WebHeaderCollection();
            HttpItem Http = new HttpItem()
            {
                URL = $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent?key={ApiKey}",
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/132.0.0.0 Safari/537.36",
                Method = "Post",
                Header = Headers,
                Accept = "*/*",
                Postdata = GetJson,
                Cookie = "",
                ContentType = "application/json; charset=utf-8",
                Encoding = Encoding.UTF8,
                Timeout = ConfigRef.GlobalRequestTimeOut,
                WebProxy = ProxyRef
            };
            try
            {
                Http.Header.Add("Accept-Encoding", " gzip");
            }
            catch { }

            string GetResult = new HttpHelper().GetHtml(Http).Html;

            Recv = GetResult;
            try
            {
                return JsonConvert.DeserializeObject<GeminiRootobject>(GetResult);
            }
            catch
            {
                return null;
            }
        }
    }
}
