using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PhoenixEngine.DataBaseManagement;
using PhoenixEngine.TranslateCore;

namespace PhoenixEngine.Engine
{
    // Copyright (C) 2025 YD525
    // Licensed under the GNU GPLv3
    // See LICENSE for details
    //https://github.com/YD525/PhoenixEngine


    public class ThreadUsageInfo
    {
        public int CurrentThreads { get; set; } = 0;
        public int MaxThreads { get; set; } = 0;
    }

    public class EngineConfig
    {
        #region RequestConfig

        /// <summary>
        /// Configured proxy IP address for network requests.
        /// </summary>
        public static string ProxyIP { get; set; } = "";

        /// <summary>
        /// Global maximum timeout duration (in milliseconds) for network requests.
        /// </summary>
        public static int GlobalRequestTimeOut { get; set; } = 8000;

        #endregion

        #region DataBase

        /// <summary>
        /// Default page size for pagination.  
        /// Represents how many items are shown per page by default.
        /// </summary>
        public static int DefPageSize { get; set; } = 0;

        /// <summary>
        /// Instance of the local SQLite database helper.
        /// Represents the pointer/reference to the current local database.
        /// </summary>
        public static SQLiteHelper LocalDB = new SQLiteHelper();

        #endregion

        #region Translation Param

        /// <summary>
        /// The name of the current mod being translated (e.g., "xxx.esp").
        /// </summary>
        public static string CurrentModName { get; set; } = "";

        /// <summary>
        /// The source language of the text to be translated.
        /// </summary>
        public static Languages SourceLanguage { get; set; } = Languages.Null;

        /// <summary>
        /// The target language for translation.
        /// </summary>
        public static Languages TargetLanguage { get; set; } = Languages.Null;

        #endregion

        #region Platform Enable State

        /// <summary>
        /// Flags indicating whether each AI or translation platform is enabled.
        /// Multiple platforms can be enabled simultaneously, and the system will perform load balancing among them.
        /// </summary>

        public static bool ChatGptApiEnable { get; set; } = false;
        public static bool GeminiApiEnable { get; set; } = false;
        public static bool CohereApiEnable { get; set; } = false;
        public static bool DeepSeekApiEnable { get; set; } = false;
        public static bool BaichuanApiEnable { get; set; } = false;
        public static bool GoogleYunApiEnable { get; set; } = false;
        public static bool DivCacheEngineEnable { get; set; } = false;
        public static bool LMLocalAIEngineEnable { get; set; } = false;
        public static bool DeepLApiEnable { get; set; } = false;

        #endregion

        #region ApiKey Set

        /// <summary>
        /// Stores API keys and model names for various translation and AI platforms.
        /// These keys must be obtained from the respective service providers.
        /// </summary>

        /// <summary>
        /// Google Translate API key.
        /// </summary>
        public static string GoogleApiKey { get; set; } = "";

        /// <summary>
        /// OpenAI ChatGPT API key.
        /// </summary>
        public static string ChatGptKey { get; set; } = "";

        /// <summary>
        /// Model name for ChatGPT (e.g., gpt-4o-mini).
        /// </summary>
        public static string ChatGptModel { get; set; } = "gpt-4o-mini";

        /// <summary>
        /// Google Gemini API key.
        /// </summary>
        public static string GeminiKey { get; set; } = "";

        /// <summary>
        /// Model name for Gemini (e.g., gemini-2.0-flash).
        /// </summary>
        public static string GeminiModel { get; set; } = "gemini-2.0-flash";

        /// <summary>
        /// DeepSeek API key.
        /// </summary>
        public static string DeepSeekKey { get; set; } = "";

        /// <summary>
        /// Model name for DeepSeek (e.g., deepseek-chat).
        /// </summary>
        public static string DeepSeekModel { get; set; } = "deepseek-chat";

        /// <summary>
        /// Baichuan API key.
        /// </summary>
        public static string BaichuanKey { get; set; } = "";

        /// <summary>
        /// Model name for Baichuan (e.g., Baichuan4-Turbo).
        /// </summary>
        public static string BaichuanModel { get; set; } = "Baichuan4-Turbo";

        /// <summary>
        /// Cohere API key.
        /// </summary>
        public static string CohereKey { get; set; } = "";

        /// <summary>
        /// DeepL Translate API key.
        /// </summary>
        public static string DeepLKey { get; set; } = "";


        public static bool IsFreeDeepL { get; set; } = true;

        /// <summary>
        /// LM Studio
        /// </summary>
        public static string LMHost { get; set; } = "http://localhost";
        public static int LMPort { get; set; } = 1234;
        public static string LMQueryParam { get; set; } = "/v1/chat/completions";
        public static string LMModel { get; set; } = "google/gemma-3-12b";

        #endregion

        #region EngineSetting

        /// <summary>
        /// The ratio of the maximum thread count at which throttling is triggered. 
        /// Range is 0 to 1, default is 0.5 meaning throttling starts when over 50% usage.
        /// </summary>
        public static double ThrottleRatio { get; set; } = 0.5;

        /// <summary>
        /// The sleep time in milliseconds for the main thread during throttling. Default is 200ms.
        /// </summary>
        public static int ThrottleDelayMs { get; set; } = 200;

        /// <summary>
        /// Specifies the maximum number of threads to use for processing.
        /// This value determines the upper limit of concurrent threads the system can use.
        /// </summary>

        public static int MaxThreadCount { get; set; } = 2;

        /// <summary>
        /// Indicates whether to automatically set the maximum number of threads.
        /// If true, the system will determine and apply a suitable thread limit based on hardware or configuration.
        /// </summary>
        public static bool AutoSetThreadLimit { get; set; } = true;

        /// <summary>
        /// Indicates whether to enable context-based generation.
        /// If true, the process will consider contextual information;  
        /// if false, it will only handle the current string without any context.
        /// </summary>
        public static bool ContextEnable { get; set; } = true;

        /// <summary>
        /// Specifies the maximum number of context entries to include during generation.
        /// For example, if set to 3, up to 3 context lines will be used.
        /// </summary>
        public static int ContextLimit { get; set; } = 3;

        /// <summary>
        /// User-defined custom prompt sent to the AI model.
        /// This prompt can be used to guide the AI's behavior or translation style.
        /// </summary>
        public static string UserCustomAIPrompt { get; set; } = "";

        #endregion
    }
}
