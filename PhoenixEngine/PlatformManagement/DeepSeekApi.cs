using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.RequestManagement;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManage;
using static PhoenixEngine.EngineManagement.DataTransmission;
using static PhoenixEngine.TranslateManage.TransCore;

namespace PhoenixEngine.PlatformManagement
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


    public class DeepSeekApi
    {
        //"Important: When translating, strictly keep any text inside angle brackets (< >) or square brackets ([ ]) unchanged. Do not modify, translate, or remove them.\n\n"
        public string QuickTrans(List<string> CustomWords,string TransSource, Languages FromLang, Languages ToLang,bool UseAIMemory,int AIMemoryCountLimit, string AIParam, ref AICall Call,string Type)
        {
            List<string> Related = new List<string>();

            if (EngineConfig.ContextEnable && UseAIMemory)
            {
                Related = EngineSelect.AIMemory.FindRelevantTranslations(FromLang, ToLang, TransSource, AIMemoryCountLimit);
            }

            if (EngineConfig.UserCustomAIPrompt.Trim().Length > 0)
            {
                AIParam = AIParam + "\n" + EngineConfig.UserCustomAIPrompt;
            }

            var GetTransSource = AIPrompt.GenerateTranslationPrompt(FromLang, ToLang, TransSource, Type, Related, CustomWords, AIParam);

            string Send = GetTransSource;
            string Recv = "";
            var GetResult = CallAI(Send, ref Recv);

            Call = new AICall(PlatformType.DeepSeek, Send, Recv);

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
            return string.Empty;
        }

        public DeepSeekRootobject CallAI(string Msg, ref string Recv)
        {
            int GetCount = Msg.Length;
            DeepSeekItem NDeepSeekItem = new DeepSeekItem();
            NDeepSeekItem.model = EngineConfig.DeepSeekModel;
            NDeepSeekItem.messages = new List<DeepSeekMessage>();
            NDeepSeekItem.messages.Add(new DeepSeekMessage("user", Msg));
            NDeepSeekItem.stream = false;
            var GetResult = CallAI(NDeepSeekItem,ref Recv);
            return GetResult;
        }

        public DeepSeekRootobject CallAI(DeepSeekItem Item, ref string Recv)
        {
            string GetJson = JsonConvert.SerializeObject(Item);
            WebHeaderCollection Headers = new WebHeaderCollection();
            Headers.Add("Authorization", string.Format("Bearer {0}", EngineConfig.DeepSeekKey));
            HttpItem Http = new HttpItem()
            {
                URL = "https://api.deepseek.com/chat/completions",
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/132.0.0.0 Safari/537.36",
                Method = "Post",
                Header = Headers,
                Accept = "*/*",
                Postdata = GetJson,
                Cookie = "",
                ContentType = "application/json",
                Timeout = EngineConfig.GlobalRequestTimeOut,
                WebProxy = ProxyCenter.CurrentProxy
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
                return JsonConvert.DeserializeObject<DeepSeekRootobject>(GetResult);
            }
            catch 
            {
                return null; 
            }
        }
    }
}
