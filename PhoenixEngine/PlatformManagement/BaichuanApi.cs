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
    public class BaichuanItem
    {
        public BaichuanMessage[] messages { get; set; }
        public string model { get; set; } = "Baichuan4-Turbo";
        public bool stream { get; set; } = false;
    }

    public class BaichuanMessage
    {
        public string role { get; set; } = "user";
        public string content { get; set; } = "";
    }

    public class BaichuanResult
    {
        public string id { get; set; }
        public string _object { get; set; }
        public int created { get; set; }
        public string model { get; set; }
        public BaichuanChoice[] choices { get; set; }
        public BaichuanUsage usage { get; set; }
    }

    public class BaichuanUsage
    {
        public int prompt_tokens { get; set; }
        public int completion_tokens { get; set; }
        public int total_tokens { get; set; }
        public int search_count { get; set; }
    }

    public class BaichuanChoice
    {
        public int index { get; set; }
        public BaichuanMessageR message { get; set; }
        public string finish_reason { get; set; }
    }

    public class BaichuanMessageR
    {
        public string role { get; set; }
        public string content { get; set; }
    }



    public class BaichuanApi
    {
        //"Important: When translating, strictly keep any text inside angle brackets (< >) or square brackets ([ ]) unchanged. Do not modify, translate, or remove them.\n\n"
        public string QuickTrans(List<string> CustomWords, string TransSource, Languages FromLang, Languages ToLang, bool UseAIMemory, int AIMemoryCountLimit, string AIParam,ref AICall Call)
        {
            List<string> Related = new List<string>();
            if (EngineConfig.ContextEnable && UseAIMemory)
            {
                Related = EngineSelect.AIMemory.FindRelevantTranslations(FromLang, TransSource, AIMemoryCountLimit);
            }

            var GetTransSource = $"You are a professional translation AI.Translate the following text from {LanguageHelper.ToLanguageCode(FromLang)} to {LanguageHelper.ToLanguageCode(ToLang)}:\n\n";

            if (AIParam.Trim().Length > 0)
            {
                GetTransSource += AIParam;
            }

            if (ConvertHelper.ObjToStr(EngineConfig.UserCustomAIPrompt).Trim().Length > 0)
            {
                GetTransSource += EngineConfig.UserCustomAIPrompt + "\n\n";
            }

            if (Related.Count > 0 || CustomWords.Count > 0)
            {
                GetTransSource += "Use the following terminology references to help you translate the text consistently:\n";
                foreach (var related in Related)
                {
                    GetTransSource += $"- {related}\n";
                }
                foreach (var Word in CustomWords)
                {
                    GetTransSource += $"- {Word}\n";
                }
                GetTransSource += "\n";
            }

            GetTransSource += $"\"\"\"\n{TransSource}\n\"\"\"\n\n";

            GetTransSource += "If translation is not possible, just return the original text exactly as-is. Do not modify.\n\n";

            GetTransSource += "Respond strictly with: {\"translation\": \"....\"}\n";
            GetTransSource += "The value must contain only translated text.\n";

            if (GetTransSource.EndsWith("\n"))
            {
                GetTransSource = GetTransSource.Substring(0, GetTransSource.Length - 1);
            }

            string Send = GetTransSource;
            string Recv = "";
            var GetResult = CallAI(Send, ref Recv);

            Call = new AICall(PlatformType.Baichuan, Send, Recv);

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

        public BaichuanResult? CallAI(string Msg,ref string Recv)
        {
            int GetCount = Msg.Length;
            BaichuanItem NBaichuanItem = new BaichuanItem();
            BaichuanMessage NBaichuanMessage = new BaichuanMessage();
            NBaichuanMessage.content = Msg;
            NBaichuanItem.messages = new BaichuanMessage[1] { NBaichuanMessage };
            NBaichuanItem.model = EngineConfig.BaichuanModel;
            var GetResult = CallAI(NBaichuanItem,ref Recv);
            return GetResult;
        }

        public BaichuanResult? CallAI(BaichuanItem Item,ref string Recv)
        {
            string GetJson = JsonSerializer.Serialize(Item);
            WebHeaderCollection Headers = new WebHeaderCollection();
            Headers.Add("Authorization", string.Format("Bearer {0}", EngineConfig.BaichuanKey));
            HttpItem Http = new HttpItem()
            {
                URL = "https://api.baichuan-ai.com/v1/chat/completions",
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
                return JsonSerializer.Deserialize<BaichuanResult>(GetResult);
            }
            catch
            {
                return null;
            }
        }
    }
}
