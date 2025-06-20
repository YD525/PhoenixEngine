using System;
using System.Collections.Generic;
using System.Data.Entity.Core.Metadata.Edm;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using PhoenixEngine.DataBaseManagement;
using PhoenixEngine.Engine;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManage;
using PhoenixEngine.TranslateManagement;
using static System.Net.Mime.MediaTypeNames;
using static PhoenixEngine.SSEATComBridge.InteropDispatcher;

namespace PhoenixEngine.SSEATComBridge
{
    
    /// <summary>
    /// For SSEAT
    /// </summary>
    [ComVisible(true)]
    public class InteropDispatcher
    {
        public static void FormatData()
        {
            Translator.FormatData();
        }

        public static void ClearCache()
        {
            Translator.ClearCache();
        }

        public static string? GetTranslatorCache(string Key)
        {
            if (Translator.TransData.ContainsKey(Key))
            {
                return Translator.TransData[Key];
            }
            else
            {
                return null;
            }
        }

        public static string GetTransData(string Key)
        {
            var GetResult = GetTranslatorCache(Key);
            if (GetResult != null)
            {
                return GetResult;
            }
            else
            {
                Translator.TransData.Add(Key, string.Empty);
            }
            return string.Empty;
        }

        public static void SetTransData(string Key, string Value)
        {
            if (Translator.TransData.ContainsKey(Key))
            {
                Translator.TransData[Key] = Value;
            }
            else
            {
                Translator.TransData.Add(Key, Value);
            }
        }

        public static int ConvertLangToID(string Lang)
        { 
            Languages GetLang = (Languages)Enum.Parse(typeof(Languages), Lang);
            return (int)GetLang;
        }

        public static string translate(string Key,string Text,int Src,int Dst)
        {
            try 
            {
                if (Key.Trim().Length == 0 || Key == null)
                {
                    throw new Exception("Key is Null!");
                }

                bool CanSleep = true;
                return Translator.QuickTrans(EngineConfig.CurrentModName,string.Empty,Key,Text,(Languages)Src, (Languages)Dst,ref CanSleep);
            }
            catch (Exception e) 
            {
                throw;
            }
        }

        public static ConfigJson ?CurrentConfig = new ConfigJson();
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
        public static string get_config()
        {
            return JsonSerializer.Serialize(CurrentConfig);
        }
        public static void set_config(string Json)
        {
            CurrentConfig = JsonSerializer.Deserialize<ConfigJson>(Json);
            if (CurrentConfig == null)
            {
                CurrentConfig = new ConfigJson();
                return;
            }

            // RequestConfig
            EngineConfig.ProxyIP = CurrentConfig.ProxyIP;
            EngineConfig.GlobalRequestTimeOut = CurrentConfig.GlobalRequestTimeOut;

            // Translation Param
            EngineConfig.CurrentModName = CurrentConfig.CurrentModName;
            EngineConfig.SourceLanguage = CurrentConfig.SourceLanguage;
            EngineConfig.TargetLanguage = CurrentConfig.TargetLanguage;

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
        }
    }
}
