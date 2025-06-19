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
        public int CurrentThreads = 0;
        public int MaxThreads = 0;
    }

    public class EngineConfig
    {
        #region RequestConfig

        public static string ProxyIP { get; set; } = "";

        public static int GlobalRequestTimeOut = 8000;

        #endregion

        #region DataBase

        public static int DefPageSize = 0;
        public static SQLiteHelper LocalDB = new SQLiteHelper();

        #endregion
        #region Translation Param

        public static string CurrentModName = "";

        public static Languages SourceLanguage = Languages.Null;

        public static Languages TargetLanguage = Languages.Null;

        #endregion

        #region Platform Enable State
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
        public static string GoogleApiKey { get; set; } = "";
        public static string ChatGptKey { get; set; } = "";
        public static string ChatGptModel { get; set; } = "gpt-4o-mini";
        public static string GeminiKey { get; set; } = "";
        public static string GeminiModel { get; set; } = "gemini-2.0-flash";
        public static string DeepSeekKey { get; set; } = "";
        public static string DeepSeekModel { get; set; } = "deepseek-chat";
        public static string BaichuanKey { get; set; } = "";
        public static string BaichuanModel { get; set; } = "Baichuan4-Turbo";
        public static string CohereKey { get; set; } = "";
        public static string DeepLKey { get; set; } = "";
        public static string UserCustomAIPrompt { get; set; } = "";
        public static bool IsFreeDeepL { get; set; } = true;
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
        public static int ContextLimit { get; set; } = 3;
        public static int MaxThreadCount { get; set; } = 2;
        public static bool AutoSetThreadLimit { get; set; } = true;
        public static bool UsingContext { get; set; } = true;
        #endregion
    }
}
