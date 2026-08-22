using System.Collections.Generic;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using PhoenixEngine.Engine;
using PhoenixEngine.Language;
using PhoenixEngine.Memory;
using PhoenixEngine.P_Delegate;
using PhoenixEngine.Request;
using PhoenixEngine.Translate;
using PhoenixEngine.Unit;

namespace PhoenixEngine.Platform
{
    public class DeepSeekItem
    {
        public string model { get; set; }
        public List<DeepSeekMessage> messages { get; set; }
        public bool stream { get; set; }
    }

    public class DeepSeekMessage
    {
        public string role { get; set; }
        public string content { get; set; }

        public DeepSeekMessage(string role, string content)
        {
            this.role = role;
            this.content = content;
        }
    }


    public class DeepSeekRootobject
    {
        public string id { get; set; }
        public string _object { get; set; }
        public int created { get; set; }
        public string model { get; set; }
        public DeepSeekChoice[] choices { get; set; }
        public DeepSeekUsage usage { get; set; }
        public string system_fingerprint { get; set; }
    }

    public class DeepSeekUsage
    {
        public int prompt_tokens { get; set; }
        public int completion_tokens { get; set; }
        public int total_tokens { get; set; }
        public DeepSeekPrompt_Tokens_Details prompt_tokens_details { get; set; }
        public int prompt_cache_hit_tokens { get; set; }
        public int prompt_cache_miss_tokens { get; set; }
    }

    public class DeepSeekPrompt_Tokens_Details
    {
        public int cached_tokens { get; set; }
    }

    public class DeepSeekChoice
    {
        public int index { get; set; }
        public DeepSeekRMessage message { get; set; }
        public object logprobs { get; set; }
        public string finish_reason { get; set; }
    }

    public class DeepSeekRMessage
    {
        public string role { get; set; }
        public string content { get; set; }
    }

    public class DeepSeekApi: I_AI_TranslationNode
    {
        private static readonly HttpHelper HttpTransport = new HttpHelper();

        public static PlatformType Type = PlatformType.DeepSeek;
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
        public string QuickTrans(string ApiKey,List<ReplaceTag> CustomWords,UnitGroup Source, Languages FromLang, Languages ToLang,bool UseAIMemory,int AIMemoryCountLimit, string AIParam, ref AICall Call)
        {
            List<string> Related = new List<string>();

            bool CanTrans = false;
            string TransSource = Source.GenContent(ref CanTrans);
            if (!CanTrans)
            {
                return "<empty>";
            }

            if (ConfigRef.ContextEnable && UseAIMemory)
            {
                Related = Source.QueryAIMemory(FromLang,ToLang,AIMemoryCountLimit);
            }

            if (ConfigRef.UserCustomAIPrompt.Trim().Length > 0)
            {
                AIParam = AIParam + "\n" + ConfigRef.UserCustomAIPrompt;
            }

            var GetTransSource = AIPrompt.GenerateTranslationPrompt(FromLang, ToLang, TransSource,Related, CustomWords, AIParam);

            string Send = GetTransSource;
            string Recv = "";
            var GetResult = CallAI(ApiKey,Send, ref Recv);

            Call = new AICall(PlatformType.DeepSeek, Send, Recv,0);

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

        public DeepSeekRootobject CallAI(string ApiKey,string Msg, ref string Recv)
        {
            this.Model = Phoenix.Config.GetPlatformData(DeepSeekApi.Type).Model;
            int GetCount = Msg.Length;
            DeepSeekItem NDeepSeekItem = new DeepSeekItem();
            NDeepSeekItem.model = Model;
            NDeepSeekItem.messages = new List<DeepSeekMessage>();
            NDeepSeekItem.messages.Add(new DeepSeekMessage("user", Msg));
            NDeepSeekItem.stream = false;
            var GetResult = CallAI(ApiKey,NDeepSeekItem,ref Recv);
            return GetResult;
        }

        public DeepSeekRootobject CallAI(string ApiKey,DeepSeekItem Item, ref string Recv)
        {
            string GetJson = JsonConvert.SerializeObject(Item);
            WebHeaderCollection Headers = new WebHeaderCollection();
            Headers.Add("Authorization", string.Format("Bearer {0}", ApiKey));
            HttpItem Http = new HttpItem()
            {
                URL = "https://api.deepseek.com/chat/completions",
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
                return JsonConvert.DeserializeObject<DeepSeekRootobject>(GetResult);
            }
            catch 
            {
                return null; 
            }
        }
    }
}
