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
        public void GetCurrentModel()
        {
            EngineConfig.LMModel = string.Empty;

            new Thread(() => { 
                EngineConfig.LMModel = GetCurrentModelName();
                EngineConfig.Save();
            }).Start();
        }
        public OpenAIResponse? CallAI(string Msg,ref string Recv)
        {
            if (EngineConfig.LMModel == string.Empty)
            {
                return new OpenAIResponse();
            }

            int GetCount = Msg.Length;
            OpenAIItem NOpenAIItem = new OpenAIItem(EngineConfig.LMModel);
            NOpenAIItem.store = true;
            NOpenAIItem.messages.Add(new OpenAIMessage("user", Msg));
            var GetResult = CallAI(NOpenAIItem,ref Recv);
            return GetResult;
        }

        public string GetCurrentModelName()
        {
            // Construct the URL for the request
            string GenUrl = EngineConfig.LMHost + ":" + EngineConfig.LMPort + "/v1/models";

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
                Timeout = 7000,
                ContentType = "application/json",
                //ProxyIp = ProxyCenter.GlobalProxyIP // Uncomment if a proxy is needed
            };

            try
            {
                string GetResult = new HttpHelper().GetHtml(Http).Html;

                var Response = JsonSerializer.Deserialize<OpenAIResponse>(GetResult);
                
                if (Response != null)
                {
                    var Json = JsonSerializer.Deserialize<JsonElement>(GetResult);

                    var Models = Json.GetProperty("data").EnumerateArray();

                    // If there are models, return the "id" (model name) of the first model
                    foreach (var Model in Models)
                    {
                        string ?ID = Model.GetProperty("id").GetString();

                        if (ID != null)
                        {
                            return ID;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting current model: {ex.Message}");
                return string.Empty;
            }

            return string.Empty;
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
                ContentType = "application/json",
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
        public string QuickTrans(List<string> CustomWords, string TransSource, Languages FromLang, Languages ToLang, bool UseAIMemory, int AIMemoryCountLimit, string AIParam,ref AICall Call,string Type)
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

            var GetTransSource = AIPrompt.GenerateTranslationPrompt(FromLang,ToLang,TransSource,Type, Related,CustomWords, AIParam);
            
            string Send = GetTransSource;
            string Recv = "";
            var GetResult = CallAI(Send,ref Recv);

            Call = new AICall(PlatformType.LMLocalAI, Send,Recv);

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
