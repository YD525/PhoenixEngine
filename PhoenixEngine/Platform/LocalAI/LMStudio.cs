using System;
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

namespace PhoenixEngine.Platform.LocalAI
{
    public class LMStudio : I_Local_AI_TranslationNode
    {
        private static readonly HttpHelper HttpTransport = new HttpHelper();

        public AITranslationMemory AIMemoryRef { get; set; } = null;
        public EngineConfigJson ConfigRef { get; set; } = null;
        public int LocalPort { get; set; } = 0;
        public int CustomID { get; set; } = 0;
        public void Init(int CustomID, AITranslationMemory AIMemory,EngineConfigJson Config)
        {
            this.CustomID = CustomID;
            this.AIMemoryRef = AIMemory;
            this.ConfigRef = Config;

            this.LocalPort = ConfigRef.GetPlatformData(LMStudio.Type).LocalPort;
        }

        public static PlatformType Type = PlatformType.LMLocalAI;
        public static string CurrentModel = "";
        public void GetCurrentModel()
        {
            if (LMStudio.CurrentModel == "")
            {
                LMStudio.CurrentModel = GetCurrentModelName();
            }
        }
        public OpenAIResponse CallAI(string Msg,ref string Recv)
        {
            if (CurrentModel == string.Empty)
            {
                GetCurrentModel();
            }

            if (CurrentModel == string.Empty)
            {
                return new OpenAIResponse();
            }

            int GetCount = Msg.Length;
            OpenAIItem NOpenAIItem = new OpenAIItem(CurrentModel);
            NOpenAIItem.store = true;
            NOpenAIItem.messages.Add(new OpenAIMessage("user", Msg));
            var GetResult = CallAI(NOpenAIItem,ref Recv);
            return GetResult;
        }

        public string GetCurrentModelName()
        {
            // Construct the URL for the request
            string GenUrl = "http://localhost" + ":" + LocalPort + "/v1/models";

            WebHeaderCollection Headers = new WebHeaderCollection();
            HttpItem Http = new HttpItem()
            {
                URL = GenUrl,
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/132.0.0.0 Safari/537.36",
                Method = "Get",
                Header = Headers,
                Accept = "*/*",
                Postdata = "",
                Cookie = "",
                Timeout = 5000,
                ContentType = "application/json",
                MaximumResponseBytes = JsonPayload.MaximumDocumentBytes,
                //ProxyIp = ProxyCenter.GlobalProxyIP // Uncomment if a proxy is needed
            };

            string getResult = HttpTransport.GetHtml(Http).Html;
            OpenAIModelListResponse result;
            return TryParseModelResponse(getResult, out result) ? result.data[0].id : string.Empty;
        }

        public static object SingleLock = new object();
        public OpenAIResponse CallAI(OpenAIItem Item,ref string Recv)
        {
            lock (SingleLock)
            {
                string GenUrl = "http://localhost" + ":" + LocalPort + "/v1/chat/completions";
                string GetJson = JsonConvert.SerializeObject(Item);
                WebHeaderCollection Headers = new WebHeaderCollection();
                //Headers.Add("Authorization", string.Format("Bearer {0}", DeFine.GlobalLocalSetting.LMKey));
                HttpItem Http = new HttpItem()
                {
                    URL = GenUrl,
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/132.0.0.0 Safari/537.36",
                    Method = "Post",
                    Header = Headers,
                    Accept = "*/*",
                    Postdata = GetJson,
                    Cookie = "",
                    ContentType = "application/json; charset=utf-8",
                    Encoding = Encoding.UTF8,
                    MaximumResponseBytes = JsonPayload.MaximumDocumentBytes
                    //ProxyIp = ProxyCenter.GlobalProxyIP
                };
                try
                {
                    Http.Header.Add("Accept-Encoding", " gzip");
                }
                catch { }

                string GetResult = HttpTransport.GetHtml(Http).Html;
                Recv = GetResult;

                OpenAIResponse result;
                return TryParseResponse(GetResult, out result) ? result : null;
            }
        }

        /// <summary>Parses a bounded local model-list response and validates its first identifier.</summary>
        /// <param name="json">The untrusted local provider response.</param>
        /// <param name="result">Receives the validated response, or <c>null</c> on failure.</param>
        /// <returns><c>true</c> when the response contains a non-empty first model identifier.</returns>
        internal static bool TryParseModelResponse(string json, out OpenAIModelListResponse result)
        {
            return JsonPayload.TryDeserialize(
                json,
                value => value != null &&
                    value.data != null &&
                    value.data.Length > 0 &&
                    value.data[0] != null &&
                    !string.IsNullOrWhiteSpace(value.data[0].id),
                out result);
        }

        /// <summary>Parses a bounded local chat response and validates the required translation fields.</summary>
        /// <param name="json">The untrusted local provider response.</param>
        /// <param name="result">Receives the validated response, or <c>null</c> on failure.</param>
        /// <returns><c>true</c> when the response contains a non-empty first message.</returns>
        internal static bool TryParseResponse(string json, out OpenAIResponse result)
        {
            return JsonPayload.TryDeserialize(
                json,
                value => value != null &&
                    value.choices != null &&
                    value.choices.Length > 0 &&
                    value.choices[0] != null &&
                    value.choices[0].message != null &&
                    !string.IsNullOrWhiteSpace(value.choices[0].message.content),
                out result);
        }
        //"Important: When translating, strictly keep any text inside angle brackets (< >) or square brackets ([ ]) unchanged. Do not modify, translate, or remove them.\n\n"
        public string QuickTrans(List<ReplaceTag> CustomWords,UnitGroup Source, Languages FromLang, Languages ToLang, bool UseAIMemory, int AIMemoryCountLimit, string AIParam,ref AICall Call)
        {
            List<string> Related = new List<string>();

            if (ConfigRef.ContextEnable && UseAIMemory)
            {
                Related = Source.QueryAIMemory(FromLang, ToLang,AIMemoryCountLimit);
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

            var GetTransSource = AIPrompt.GenerateTranslationPrompt(FromLang,ToLang,TransSource,Related,CustomWords, AIParam);
            
            string Send = GetTransSource;
            string Recv = "";
            var GetResult = CallAI(Send,ref Recv);

            Call = new AICall(PlatformType.LMLocalAI, Send,Recv,0);

            if (GetResult != null)
            {
                if (GetResult.choices != null)
                {
                    string GetStr = "";
                    if (GetResult.choices.Length > 0)
                    {
                        GetStr = GetResult.choices[0].message.content.Trim();
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
            return string.Empty;
        }
    }
}
