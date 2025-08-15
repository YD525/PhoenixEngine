using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManage;
using PhoenixEngine.TranslateManagement;

namespace PhoenixEngine.SSEATComBridge
{
   
    public class BookTransingItem
    {
        public Thread ?CurrentThread = null;
        public TextSegmentTranslator Translator = null;
        public CancellationToken EndCall;
    }
    public class BridgeHelper
    {
        public static List<BookTransingItem> BookTransTrds = new List<BookTransingItem>();

        public static bool IsInit = false;

        public static ConfigJson? CurrentConfig = new ConfigJson();
        public class ConfigJson
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
            /// The source language of the text to be translated.
            /// </summary>
            public Languages SourceLanguage { get; set; } = Languages.Null;

            /// <summary>
            /// The target language for translation.
            /// </summary>
            public Languages TargetLanguage { get; set; } = Languages.Null;

            #endregion

            #region Platform Enable State

            public bool PreTranslateEnable { get; set; } = false;

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
            public bool LMLocalAIEnable { get; set; } = false;
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
        public static void SyncCurrentConfig()
        {
            if (CurrentConfig != null)
            {
                CurrentConfig.ProxyIP = EngineConfig.ProxyIP;
                CurrentConfig.GlobalRequestTimeOut = EngineConfig.GlobalRequestTimeOut;

                CurrentConfig.PreTranslateEnable = EngineConfig.PreTranslateEnable;

                CurrentConfig.ChatGptApiEnable = EngineConfig.ChatGptApiEnable;
                CurrentConfig.GeminiApiEnable = EngineConfig.GeminiApiEnable;
                CurrentConfig.CohereApiEnable = EngineConfig.CohereApiEnable;
                CurrentConfig.DeepSeekApiEnable = EngineConfig.DeepSeekApiEnable;
                CurrentConfig.BaichuanApiEnable = EngineConfig.BaichuanApiEnable;
                CurrentConfig.GoogleYunApiEnable = EngineConfig.GoogleYunApiEnable;
               
                CurrentConfig.LMLocalAIEnable = EngineConfig.LMLocalAIEnable;
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
    }
    public class BookTransListJson
    {
        public string ModName = "";
        public string Key = "";
        public int CurrentThreadCount = 0;
        public string Text = "";
        public bool IsEnd = false;
        public int State = 0;
    }
    public class EndBookTranslationJson
    {
        public int ThreadID { get; set; } = 0;
    }
    public class BookTransItemJson
    {
        public string Key { get; set; } = "";
        public string Source { get; set; } = "";
    }
    public class StartTranslationJson
    { 
        public int ThreadLimit { get; set; } = 2;
    }
    public class FromLanguageCodeJson
    {
        public string Lang { get; set; } = "";
    }
    public class ConfigLanguageJson
    {
        public int Src { get; set; } = 0;
        public int Dst { get; set; } = 0;
    }

    public class TranslationUnitJson
    {
        public string ModName { get; set; } = "";
        public double Score { get; set; } = 100;
        public string Key { get; set; } = "";
        public string Type { get; set; } = "";
        public string SourceText { get; set; } = "";
        public string TransText { get; set; } = "";
        public bool IsDuplicateSource { get; set; } = false;
        public bool Leader { get; set; } = false;
        public bool Translated { get; set; } = false;
    }

    public class TranslatedResultJson
    {
        public TranslationUnitJson? Item { get; set; } = null;
        public int State { get; set; } = 0;
    }

    public class AdvancedDictionaryJson
    {
        public string TargetModName { get; set; } = "";  // Optional: Target mod name, empty means no restriction
        public string Type { get; set; } = "";           // Optional: Type filter, empty means no restriction

        /// <summary>
        /// Required parameter: The source text to be matched.
        /// </summary>
        public string Source { get; set; } = "";

        /// <summary>
        /// Required parameter: The translation or replacement result.
        /// </summary>
        public string Result { get; set; } = "";

        /// <summary>
        /// Required parameter: Source language code.
        /// </summary>
        public int From { get; set; } = 0;

        /// <summary>
        /// Required parameter: Target language code.
        /// </summary>
        public int To { get; set; } = 0;

        public int ExactMatch { get; set; } = 0;  // Optional: 1 to enable exact match, 0 otherwise
        public int IgnoreCase { get; set; } = 0;  // Optional: 1 to ignore case, 0 otherwise
        public string Regex { get; set; } = "";   // Optional: Regular expression for matching, empty means no regex
    }

    public class RemoveKeyWordJson
    {
        public int rowid { get; set; } = 0;
    }

    public class QueryKeyWordJson
    {
        public int From { get; set; } = 0;
        public int To { get; set; } = 0;
        public int PageNo { get; set; } = 0;
    }
}
