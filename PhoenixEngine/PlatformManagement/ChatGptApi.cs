using System.Net;
using System.Text.Json;
using PhoenixEngine.ConvertManager;
using PhoenixEngine.DelegateManagement;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.RequestManagement;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManage;
using static PhoenixEngine.EngineManagement.DataTransmission;
using static PhoenixEngine.TranslateManage.TransCore;

namespace PhoenixEngine.PlatformManagement
{
    public class ChatGptItem
    {
        public string model { get; set; }
        public bool store { get; set; }
        public List<ChatGptMessage> messages { get; set; }
    }

    public class ChatGptMessage
    {
        public string role { get; set; }
        public string content { get; set; }

        public ChatGptMessage(string role, string content)
        {
            this.role = role;
            this.content = content;
        }
    }

    public class ChatGptApi
    {
        public ChatGptRootobject? CallAI(string Msg,ref string Recv)
        {
            int GetCount = Msg.Length; 
            ChatGptItem NChatGptItem = new ChatGptItem();
            NChatGptItem.model = EngineConfig.ChatGptModel;
            NChatGptItem.store = true;
            NChatGptItem.messages = new List<ChatGptMessage>();
            NChatGptItem.messages.Add(new ChatGptMessage("user", Msg));
            var GetResult = CallAI(NChatGptItem,ref Recv);
            return GetResult;
        }
        public void GetModes()
        {
            WebHeaderCollection Headers = new WebHeaderCollection();
            Headers.Add("Authorization", string.Format("Bearer {0}", EngineConfig.ChatGptKey));
            HttpItem Http = new HttpItem()
            {
                URL = "https://api.openai.com/v1/models",
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/132.0.0.0 Safari/537.36",
                Method = "Get",
                Header = Headers,
                Accept = "*/*",
                Postdata = "",
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
        }
        public ChatGptRootobject? CallAI(ChatGptItem Item,ref string Recv)
        {
            //GetModes();
            string GetJson = JsonSerializer.Serialize(Item);
            WebHeaderCollection Headers = new WebHeaderCollection();
            Headers.Add("Authorization", string.Format("Bearer {0}", EngineConfig.ChatGptKey));
            HttpItem Http = new HttpItem()
            {
                URL = "https://api.openai.com/v1/chat/completions",
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
                return JsonSerializer.Deserialize<ChatGptRootobject>(GetResult);
            }
            catch 
            {
                return null; 
            }
        }
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

            Call = new AICall(PlatformType.ChatGpt, Send, Recv);

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



        public class ChatGptRootobject
        {
            public string id { get; set; }
            public string _object { get; set; }
            public int created { get; set; }
            public string model { get; set; }
            public ChatChoice[] choices { get; set; }
            public ChatUsage usage { get; set; }
            public string service_tier { get; set; }
            public string system_fingerprint { get; set; }
        }

        public class ChatUsage
        {
            public int prompt_tokens { get; set; }
            public int completion_tokens { get; set; }
            public int total_tokens { get; set; }
            public ChatPrompt_Tokens_Details prompt_tokens_details { get; set; }
            public ChatCompletion_Tokens_Details completion_tokens_details { get; set; }
        }

        public class ChatPrompt_Tokens_Details
        {
            public int cached_tokens { get; set; }
            public int audio_tokens { get; set; }
        }

        public class ChatCompletion_Tokens_Details
        {
            public int reasoning_tokens { get; set; }
            public int audio_tokens { get; set; }
            public int accepted_prediction_tokens { get; set; }
            public int rejected_prediction_tokens { get; set; }
        }

        public class ChatChoice
        {
            public int index { get; set; }
            public ChatMessage message { get; set; }
            public object logprobs { get; set; }
            public string finish_reason { get; set; }
        }

        public class ChatMessage
        {
            public string role { get; set; }
            public string content { get; set; }
            public object refusal { get; set; }
        }
    }
}
