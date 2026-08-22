using System.Collections.Generic;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using PhoenixEngine.Common;
using PhoenixEngine.Engine;
using PhoenixEngine.Language;
using PhoenixEngine.Memory;
using PhoenixEngine.P_Delegate;
using PhoenixEngine.Request;
using PhoenixEngine.Translate;
using PhoenixEngine.Unit;

namespace PhoenixEngine.Platform
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
        private static readonly HttpHelper HttpTransport = new HttpHelper();

        public static PlatformType Type = PlatformType.Gemini;
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
        }

        public string QuickTrans(string ApiKey,List<ReplaceTag> CustomWords,UnitGroup Source, Languages FromLang, Languages ToLang, bool UseAIMemory, int AIMemoryCountLimit, string AIParam, ref AICall Call)
        {
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
            var GetResult = CallAI(ApiKey,Send, ref Recv);

            Call = new AICall(PlatformType.Gemini, Send, Recv,0);

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

        public GeminiRootobject CallAI(string ApiKey,string Msg, ref string Recv)
        {
            int GetCount = Msg.Length;
            GeminiItem NGeminiItem = new GeminiItem();
            NGeminiItem.contents.Add(new GeminiContent());
            NGeminiItem.contents[0].parts.Add(new GeminiPart());
            NGeminiItem.contents[0].parts[0].text = Msg;
            var GetResult = CallAI(ApiKey,NGeminiItem,ref Recv);
            return GetResult;
        }

        public GeminiRootobject CallAI(string ApiKey,GeminiItem Item, ref string Recv)
        {
            this.Model = Phoenix.Config.GetPlatformData(GeminiApi.Type).Model;

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
                MaximumResponseBytes = JsonPayload.MaximumDocumentBytes,
                WebProxy = ProxyRef
            };
            try
            {
                Http.Header.Add("Accept-Encoding", " gzip");
            }
            catch { }

            string GetResult = HttpTransport.GetHtml(Http).Html;

            Recv = GetResult;
            GeminiRootobject result;
            return TryParseResponse(GetResult, out result) ? result : null;
        }

        /// <summary>Parses a bounded Gemini response and validates the required translation fields.</summary>
        /// <param name="json">The untrusted provider response.</param>
        /// <param name="result">Receives the validated response, or <c>null</c> on failure.</param>
        /// <returns><c>true</c> when the response contains a non-empty first text part.</returns>
        internal static bool TryParseResponse(string json, out GeminiRootobject result)
        {
            return JsonPayload.TryDeserialize(
                json,
                value => value != null &&
                    value.candidates != null &&
                    value.candidates.Length > 0 &&
                    value.candidates[0] != null &&
                    value.candidates[0].content != null &&
                    value.candidates[0].content.parts != null &&
                    value.candidates[0].content.parts.Count > 0 &&
                    value.candidates[0].content.parts[0] != null &&
                    !string.IsNullOrWhiteSpace(value.candidates[0].content.parts[0].text),
                out result);
        }
    }
}
