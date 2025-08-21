using System.Net;
using System.Text.Json;
using PhoenixEngine.ConvertManager;
using PhoenixEngine.DelegateManagement;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.RequestManagement;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManage;
using static PhoenixEngine.EngineManagement.DataTransmission;
using static PhoenixEngine.PlatformManagement.LocalAI.LocalAIJson;
using static PhoenixEngine.TranslateManage.TransCore;

namespace PhoenixEngine.PlatformManagement.LocalAI
{
    public class LMStudio
    {
        public OpenAIResponse? CallAI(string Msg,ref string Recv)
        {
            int GetCount = Msg.Length;
            OpenAIItem NOpenAIItem = new OpenAIItem(EngineConfig.LMModel);
            NOpenAIItem.store = true;
            NOpenAIItem.messages.Add(new OpenAIMessage("user", Msg));
            var GetResult = CallAI(NOpenAIItem,ref Recv);
            return GetResult;
        }

        public OpenAIResponse? CallAI(OpenAIItem Item,ref string Recv)
        {
            string GenUrl = EngineConfig.LMHost + ":" + EngineConfig.LMPort + "/v1/chat/completions";
            string GetJson = JsonSerializer.Serialize(Item);
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
                ContentType = "application/json"
                //Timeout = DeFine.GlobalRequestTimeOut
                //ProxyIp = ProxyCenter.GlobalProxyIP
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
                return JsonSerializer.Deserialize<OpenAIResponse>(GetResult);
            }
            catch
            {
                return null;
            }
        }
        //"Important: When translating, strictly keep any text inside angle brackets (< >) or square brackets ([ ]) unchanged. Do not modify, translate, or remove them.\n\n"
        public string QuickTrans(List<string> CustomWords, string TransSource, Languages FromLang, Languages ToLang, bool UseAIMemory, int AIMemoryCountLimit, string AIParam,ref AICall Call)
        {
            List<string> Related = new List<string>();
            if (EngineConfig.ContextEnable && UseAIMemory)
            {
                Related = EngineSelect.AIMemory.FindRelevantTranslations(FromLang, TransSource, AIMemoryCountLimit);
            }

            var GetTransSource = $"You are a professional translation AI. Translate the following text from {LanguageHelper.ToLanguageCode(FromLang)} to {LanguageHelper.ToLanguageCode(ToLang)}. Respond ONLY with the translated content.Do not include any explanations or comments.\n";

            if (AIParam.Trim().Length > 0)
            {
                GetTransSource += AIParam;
            }

            if (ConvertHelper.ObjToStr(EngineConfig.UserCustomAIPrompt).Trim().Length > 0)
            {
                GetTransSource += EngineConfig.UserCustomAIPrompt + "\n";
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

            GetTransSource += "Respond strictly with: {\"translation\": \"....\"}\n";
            GetTransSource += "The value must contain only translated text.\n";

            if (GetTransSource.EndsWith("\n"))
            {
                GetTransSource = GetTransSource.Substring(0, GetTransSource.Length - 1);
            }

            string Send = GetTransSource;
            string Recv = "";
            var GetResult = CallAI(Send,ref Recv);

            Call = new AICall("LMLocalAI", Send,Recv);

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
    }
}
