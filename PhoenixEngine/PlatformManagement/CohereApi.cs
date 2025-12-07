
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cohere;
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
    public class CohereHelper
    {
        private readonly CohereClient _Client;

        public CohereHelper(string ApiKey)
        {
            var Handler = new HttpClientHandler
            {
                Proxy = ProxyCenter.CurrentProxy,
                UseProxy = ProxyCenter.CurrentProxy != null,
            };

            var HttpClient = new HttpClient(Handler, true)
            {
                Timeout = TimeSpan.FromMilliseconds(EngineConfig.GlobalRequestTimeOut)
            };

            _Client = new CohereClient(ApiKey,HttpClient);
        }

        public string GenerateText(string prompt,ref string Recv)
        {
            var Request = new GenerateRequest
            {
                Prompt = prompt
            };

            var Response = _Client.GenerateAsync(Request).GetAwaiter().GetResult();

            if (Response?.Generations != null)
            {
                Recv = JsonSerializer.Serialize(Response.Generations);
            }

            if (Response?.Generations != null && Response.Generations.Count > 0)
            {
                return Response.Generations[0].Text;
            }
            return string.Empty;
        }
    }
    public class CohereApi
    {
        //"Important: When translating, strictly keep any text inside angle brackets (< >) or square brackets ([ ]) unchanged. Do not modify, translate, or remove them.\n\n"
        public string QuickTrans(List<string> CustomWords,string TransSource, Languages FromLang, Languages ToLang, bool UseAIMemory, int AIMemoryCountLimit, string AIParam, ref AICall Call,string Type)
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

            Call = new AICall(PlatformType.Cohere, Send, Recv);

            if (GetResult != null)
            {
                if (GetResult.Trim().Length > 0)
                {
                    if (GetResult.Trim().Equals("<translated_text>"))
                    {
                        return string.Empty;
                    }

                    Call.Success = true;

                    return GetResult;
                }
            }
            return string.Empty;
        }

        public string CallAI(string Msg,ref string Recv)
        {
            try
            {
                var Cohere = new CohereHelper(EngineConfig.CohereKey);
                string Result = Cohere.GenerateText(Msg,ref Recv);
                return JsonGeter.GetValue(Result);
            }
            catch 
            {
                return string.Empty;
            }
        }
    }
}
