using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
                //ProxyIp = ProxyCenter.GlobalProxyIP // Uncomment if a proxy is needed
            };

            try
            {
                string GetResult = HttpTransport.GetHtml(Http).Html;
                JObject Obj = JObject.Parse(GetResult);

                JArray Models = (JArray)Obj["data"];
                if (Models != null && Models.Count > 0)
                {
                    string ID = (string)Models[0]["id"];
                    return ID ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting current model: {ex.Message}");
                return string.Empty;
            }

            return string.Empty;
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
                    Encoding = Encoding.UTF8
                    //ProxyIp = ProxyCenter.GlobalProxyIP
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
                    return JsonConvert.DeserializeObject<OpenAIResponse>(GetResult);
                }
                catch
                {
                    return null;
                }
            }
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
