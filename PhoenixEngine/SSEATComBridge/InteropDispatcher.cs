using System.Runtime.InteropServices;
using System.Text.Json;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManage;

namespace PhoenixEngine.SSEATComBridge
{
    
    /// <summary>
    /// For SSEAT
    /// </summary>
    [ComVisible(true)]
    public class InteropDispatcher
    {
        public static bool IsInit = false;

        #region Engine Config
        private static ConfigJson ?CurrentConfig = new ConfigJson();
        private class ConfigJson
        {
            #region RequestConfig

            /// <summary>
            /// Configured proxy IP address for network requests.
            /// </summary>
            public string ProxyIP { get; set; } = "";

            /// <summary>
            /// Global maximum timeout duration (in milliseconds) for network requests.
            /// </summary>
            public int GlobalRequestTimeOut { get; set; } = 8000;

            #endregion

            #region Translation Param

            /// <summary>
            /// The name of the current mod being translated (e.g., "xxx.esp").
            /// </summary>
            public string CurrentModName { get; set; } = "";

            /// <summary>
            /// The source language of the text to be translated.
            /// </summary>
            public Languages SourceLanguage { get; set; } = Languages.Null;

            /// <summary>
            /// The target language for translation.
            /// </summary>
            public Languages TargetLanguage { get; set; } = Languages.Null;

            #endregion

            #region Platform Enable State

            /// <summary>
            /// Flags indicating whether each AI or translation platform is enabled.
            /// Multiple platforms can be enabled simultaneously, and the system will perform load balancing among them.
            /// </summary>

            public bool ChatGptApiEnable { get; set; } = false;
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

            /// <summary>
            /// Stores API keys and model names for various translation and AI platforms.
            /// These keys must be obtained from the respective service providers.
            /// </summary>

            /// <summary>
            /// Google Translate API key.
            /// </summary>
            public string GoogleApiKey { get; set; } = "";

            /// <summary>
            /// OpenAI ChatGPT API key.
            /// </summary>
            public string ChatGptKey { get; set; } = "";

            /// <summary>
            /// Model name for ChatGPT (e.g., gpt-4o-mini).
            /// </summary>
            public string ChatGptModel { get; set; } = "gpt-4o-mini";

            /// <summary>
            /// Google Gemini API key.
            /// </summary>
            public string GeminiKey { get; set; } = "";

            /// <summary>
            /// Model name for Gemini (e.g., gemini-2.0-flash).
            /// </summary>
            public string GeminiModel { get; set; } = "gemini-2.0-flash";

            /// <summary>
            /// DeepSeek API key.
            /// </summary>
            public string DeepSeekKey { get; set; } = "";

            /// <summary>
            /// Model name for DeepSeek (e.g., deepseek-chat).
            /// </summary>
            public string DeepSeekModel { get; set; } = "deepseek-chat";

            /// <summary>
            /// Baichuan API key.
            /// </summary>
            public string BaichuanKey { get; set; } = "";

            /// <summary>
            /// Model name for Baichuan (e.g., Baichuan4-Turbo).
            /// </summary>
            public string BaichuanModel { get; set; } = "Baichuan4-Turbo";

            /// <summary>
            /// Cohere API key.
            /// </summary>
            public string CohereKey { get; set; } = "";

            /// <summary>
            /// DeepL Translate API key.
            /// </summary>
            public string DeepLKey { get; set; } = "";


            public bool IsFreeDeepL { get; set; } = true;

            /// <summary>
            /// LM Studio
            /// </summary>
            public string LMHost { get; set; } = "http://localhost";
            public int LMPort { get; set; } = 1234;
            public string LMQueryParam { get; set; } = "/v1/chat/completions";
            public string LMModel { get; set; } = "google/gemma-3-12b";

            #endregion

            #region EngineSetting

            /// <summary>
            /// The ratio of the maximum thread count at which throttling is triggered. 
            /// Range is 0 to 1, default is 0.5 meaning throttling starts when over 50% usage.
            /// </summary>
            public double ThrottleRatio { get; set; } = 0.5;

            /// <summary>
            /// The sleep time in milliseconds for the main thread during throttling. Default is 200ms.
            /// </summary>
            public int ThrottleDelayMs { get; set; } = 200;

            /// <summary>
            /// Specifies the maximum number of threads to use for processing.
            /// This value determines the upper limit of concurrent threads the system can use.
            /// </summary>

            public int MaxThreadCount { get; set; } = 2;

            /// <summary>
            /// Indicates whether to automatically set the maximum number of threads.
            /// If true, the system will determine and apply a suitable thread limit based on hardware or configuration.
            /// </summary>
            public bool AutoSetThreadLimit { get; set; } = true;

            /// <summary>
            /// Indicates whether to enable context-based generation.
            /// If true, the process will consider contextual information;  
            /// if false, it will only handle the current string without any context.
            /// </summary>
            public bool ContextEnable { get; set; } = true;

            /// <summary>
            /// Specifies the maximum number of context entries to include during generation.
            /// For example, if set to 3, up to 3 context lines will be used.
            /// </summary>
            public int ContextLimit { get; set; } = 3;

            /// <summary>
            /// User-defined custom prompt sent to the AI model.
            /// This prompt can be used to guide the AI's behavior or translation style.
            /// </summary>
            public string UserCustomAIPrompt { get; set; } = "";

            #endregion
        }
        private static void SyncCurrentConfig()
        {
            if (CurrentConfig != null)
            {
                CurrentConfig.ProxyIP = EngineConfig.ProxyIP;
                CurrentConfig.GlobalRequestTimeOut = EngineConfig.GlobalRequestTimeOut;

                CurrentConfig.ChatGptApiEnable = EngineConfig.ChatGptApiEnable;
                CurrentConfig.GeminiApiEnable = EngineConfig.GeminiApiEnable;
                CurrentConfig.CohereApiEnable = EngineConfig.CohereApiEnable;
                CurrentConfig.DeepSeekApiEnable = EngineConfig.DeepSeekApiEnable;
                CurrentConfig.BaichuanApiEnable = EngineConfig.BaichuanApiEnable;
                CurrentConfig.GoogleYunApiEnable = EngineConfig.GoogleYunApiEnable;
                CurrentConfig.DivCacheEngineEnable = EngineConfig.DivCacheEngineEnable;
                CurrentConfig.LMLocalAIEngineEnable = EngineConfig.LMLocalAIEngineEnable;
                CurrentConfig.DeepLApiEnable = EngineConfig.DeepLApiEnable;

                CurrentConfig.GoogleApiKey = EngineConfig.GoogleApiKey;
                CurrentConfig.ChatGptKey = EngineConfig.ChatGptKey;
                CurrentConfig.ChatGptModel = EngineConfig.ChatGptModel;
                CurrentConfig.GeminiKey = EngineConfig.GeminiKey;
                CurrentConfig.GeminiModel = EngineConfig.GeminiModel;
                CurrentConfig.DeepSeekKey = EngineConfig.DeepSeekKey;
                CurrentConfig.DeepSeekModel = EngineConfig.DeepSeekModel;
                CurrentConfig.BaichuanKey = EngineConfig.BaichuanKey;
                CurrentConfig.BaichuanModel = EngineConfig.BaichuanModel;
                CurrentConfig.CohereKey = EngineConfig.CohereKey;
                CurrentConfig.DeepLKey = EngineConfig.DeepLKey;
                CurrentConfig.IsFreeDeepL = EngineConfig.IsFreeDeepL;

                CurrentConfig.LMHost = EngineConfig.LMHost;
                CurrentConfig.LMPort = EngineConfig.LMPort;
                CurrentConfig.LMQueryParam = EngineConfig.LMQueryParam;
                CurrentConfig.LMModel = EngineConfig.LMModel;

                CurrentConfig.ThrottleRatio = EngineConfig.ThrottleRatio;
                CurrentConfig.ThrottleDelayMs = EngineConfig.ThrottleDelayMs;
                CurrentConfig.MaxThreadCount = EngineConfig.MaxThreadCount;
                CurrentConfig.AutoSetThreadLimit = EngineConfig.AutoSetThreadLimit;
                CurrentConfig.ContextEnable = EngineConfig.ContextEnable;
                CurrentConfig.ContextLimit = EngineConfig.ContextLimit;
                CurrentConfig.UserCustomAIPrompt = EngineConfig.UserCustomAIPrompt;
            }
        }
        #endregion

        #region Com
        public static void init()
        {
            Engine.Init();
            IsInit = true;
        }
        public static int from_language_code(string Lang)
        {
           return (int)LanguageHelper.FromLanguageCode(Lang);
        }
        public static string translate(string ModName,string Type,string Key, string Text, int Src, int Dst)
        {
            if (!IsInit)
            {
                throw new Exception("Initialization required.");
            }

            try
            {
                if (Key.Trim().Length == 0 || Key == null)
                {
                    throw new Exception("Key is Null!");
                }

                bool CanSleep = true;
                return Translator.QuickTrans(ModName,Type, Key, Text, (Languages)Src, (Languages)Dst, ref CanSleep);
            }
            catch (Exception e)
            {
                throw;
            }
        }
        public static string get_config()
        {
            if (!IsInit)
            {
                throw new Exception("Initialization required.");
            }

            SyncCurrentConfig();
            return JsonSerializer.Serialize(CurrentConfig);
        }
        public static bool set_config(string Json)
        {
            if (!IsInit)
            {
                throw new Exception("Initialization required.");
            }

            try
            {
                CurrentConfig = JsonSerializer.Deserialize<ConfigJson>(Json);
            }
            catch
            {
                CurrentConfig = null;
                return false;
            }

            if (CurrentConfig == null)
            {
                CurrentConfig = new ConfigJson();
                return false;
            }

            // RequestConfig
            EngineConfig.ProxyIP = CurrentConfig.ProxyIP;
            EngineConfig.GlobalRequestTimeOut = CurrentConfig.GlobalRequestTimeOut;

            // Platform Enable State
            EngineConfig.ChatGptApiEnable = CurrentConfig.ChatGptApiEnable;
            EngineConfig.GeminiApiEnable = CurrentConfig.GeminiApiEnable;
            EngineConfig.CohereApiEnable = CurrentConfig.CohereApiEnable;
            EngineConfig.DeepSeekApiEnable = CurrentConfig.DeepSeekApiEnable;
            EngineConfig.BaichuanApiEnable = CurrentConfig.BaichuanApiEnable;
            EngineConfig.GoogleYunApiEnable = CurrentConfig.GoogleYunApiEnable;
            EngineConfig.DivCacheEngineEnable = CurrentConfig.DivCacheEngineEnable;
            EngineConfig.LMLocalAIEngineEnable = CurrentConfig.LMLocalAIEngineEnable;
            EngineConfig.DeepLApiEnable = CurrentConfig.DeepLApiEnable;

            // API Key / Model
            EngineConfig.GoogleApiKey = CurrentConfig.GoogleApiKey;
            EngineConfig.ChatGptKey = CurrentConfig.ChatGptKey;
            EngineConfig.ChatGptModel = CurrentConfig.ChatGptModel;
            EngineConfig.GeminiKey = CurrentConfig.GeminiKey;
            EngineConfig.GeminiModel = CurrentConfig.GeminiModel;
            EngineConfig.DeepSeekKey = CurrentConfig.DeepSeekKey;
            EngineConfig.DeepSeekModel = CurrentConfig.DeepSeekModel;
            EngineConfig.BaichuanKey = CurrentConfig.BaichuanKey;
            EngineConfig.BaichuanModel = CurrentConfig.BaichuanModel;
            EngineConfig.CohereKey = CurrentConfig.CohereKey;
            EngineConfig.DeepLKey = CurrentConfig.DeepLKey;
            EngineConfig.IsFreeDeepL = CurrentConfig.IsFreeDeepL;

            // LM Studio
            EngineConfig.LMHost = CurrentConfig.LMHost;
            EngineConfig.LMPort = CurrentConfig.LMPort;
            EngineConfig.LMQueryParam = CurrentConfig.LMQueryParam;
            EngineConfig.LMModel = CurrentConfig.LMModel;

            // Engine Setting
            EngineConfig.ThrottleRatio = CurrentConfig.ThrottleRatio;
            EngineConfig.ThrottleDelayMs = CurrentConfig.ThrottleDelayMs;
            EngineConfig.MaxThreadCount = CurrentConfig.MaxThreadCount;
            EngineConfig.AutoSetThreadLimit = CurrentConfig.AutoSetThreadLimit;
            EngineConfig.ContextEnable = CurrentConfig.ContextEnable;
            EngineConfig.ContextLimit = CurrentConfig.ContextLimit;
            EngineConfig.UserCustomAIPrompt = CurrentConfig.UserCustomAIPrompt;

            EngineConfig.Save();

            return true;
        }
        #endregion
    }
}
