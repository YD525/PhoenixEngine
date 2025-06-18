using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PhoenixEngine.TranslateCore;

namespace PhoenixEngine.Engine
{
    public class EngineConfig
    {
        #region Platform Enable State
        public static bool ChatGptApiEnable { get; set; } = false;
        public bool GeminiApiEnable { get; set; } = false;
        public bool CohereApiEnable { get; set; } = false;
        public bool DeepSeekApiEnable { get; set; } = false;
        public bool BaichuanApiEnable { get; set; } = false;
        public bool GoogleYunApiEnable { get; set; } = false;
        public bool DivCacheEngineEnable { get; set; } = false;
        public bool LMLocalAIEngineEnable { get; set; } = false;
        public bool DeepLApiEnable { get; set; } = false;

        #endregion

        #region ApiKey Set
        public string GoogleApiKey { get; set; } = "";
        public string ChatGptKey { get; set; } = "";
        public string ChatGptModel { get; set; } = "gpt-4o-mini";
        public string GeminiKey { get; set; } = "";
        public string GeminiModel { get; set; } = "gemini-2.0-flash";
        public string DeepSeekKey { get; set; } = "";
        public string DeepSeekModel { get; set; } = "deepseek-chat";
        public string BaichuanKey { get; set; } = "";
        public string BaichuanModel { get; set; } = "Baichuan4-Turbo";
        public string CohereKey { get; set; } = "";
        public string DeepLKey { get; set; } = "";
        public string UserCustomAIPrompt { get; set; } = "";
        public bool IsFreeDeepL { get; set; } = true;
        public string LMHost { get; set; } = "http://localhost";
        public int LMPort { get; set; } = 1234;
        public string LMQueryParam { get; set; } = "/v1/chat/completions";
        public string LMModel { get; set; } = "google/gemma-3-12b";

        #endregion

        #region EngineSetting
        public int ContextLimit { get; set; } = 3;
        public string ProxyIP { get; set; } = "";
        public int TransCount { get; set; } = 0;
        public int MaxThreadCount { get; set; } = 2;
        public bool AutoSetThreadLimit { get; set; } = true;
        public bool UsingContext { get; set; } = true;

        #endregion
    }
}
