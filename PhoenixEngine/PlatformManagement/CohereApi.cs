
using System.Net.Http;
using Cohere;
using PhoenixEngine.ConvertManager;
using PhoenixEngine.DelegateManagement;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.RequestManagement;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManage;
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

        public string GenerateText(string prompt)
        {
            var Request = new GenerateRequest
            {
                Prompt = prompt
            };

            var Response = _Client.GenerateAsync(Request).GetAwaiter().GetResult();

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
        public string QuickTrans(List<string> CustomWords,string TransSource, Languages FromLang, Languages ToLang, bool UseAIMemory, int AIMemoryCountLimit, string Param)
        {
            List<string> Related = new List<string>();
            if (EngineConfig.ContextEnable && UseAIMemory)
            {
                Related = EngineSelect.AIMemory.FindRelevantTranslations(FromLang, TransSource, AIMemoryCountLimit);
            }

            var GetTransSource = $"Translate the following text from {LanguageHelper.ToLanguageCode(FromLang)} to {LanguageHelper.ToLanguageCode(ToLang)}:\n\n";

            if (Param.Trim().Length > 0)
            {
                GetTransSource += Param;
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
            GetTransSource += "Respond in JSON format: {\"translation\": \"<translated_text>\"}";

            if (GetTransSource.EndsWith("\n"))
            {
                GetTransSource = GetTransSource.Substring(0, GetTransSource.Length - 1);
            }

            var GetResult = CallAI(GetTransSource);
            if (GetResult != null)
            {
                if (GetResult.Trim().Length > 0)
                {
                    if (DelegateHelper.SetLog != null)
                    {
                        DelegateHelper.SetLog(GetTransSource + "\r\n\r\n AI(Cohere):\r\n" + GetResult,1);
                    }

                    if (GetResult.Trim().Equals("<translated_text>"))
                    {
                        return string.Empty;
                    }

                    return GetResult;
                }
            }
            return string.Empty;
        }

        public string CallAI(string Msg)
        {
            try
            {
                var Cohere = new CohereHelper(EngineConfig.CohereKey);
                string Result = Cohere.GenerateText(Msg);
                return JsonGeter.GetValue(Result);
            }
            catch 
            {
                return string.Empty;
            }
        }
    }
}
