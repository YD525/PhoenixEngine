using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PhoenixEngine.DataBaseManagement;
using PhoenixEngine.TranslateCore;

namespace PhoenixEngine.EngineManagement
{
    public class ThreadUsageInfo
    {
        public int CurrentThreads { get; set; } = 0;
        public int MaxThreads { get; set; } = 0;
    }

    public class EngineConfigJson
    {
        #region RequestConfig

        /// <summary>
        /// Configured http proxy or local proxy for network requests.
        /// </summary>
        public string ProxyUrl { get; set; } = "";

        public string ProxyUserName { get; set; } = "";

        public string ProxyPassword { get; set; } = "";

        /// <summary>
        /// Global maximum timeout duration (in milliseconds) for network requests.
        /// </summary>
        public int GlobalRequestTimeOut { get; set; } = 8000;

        #endregion

        #region DataBase

        /// <summary>
        /// Default page size for pagination.  
        /// Represents how many items are shown per page by default.
        /// </summary>
        public int DefPageSize { get; set; } = 50;

        #endregion


        public bool PreTranslateEnable { get; set; } = true;


        #region Platform Enable State

        /// <summary>
        /// Flags indicating whether each AI or translation platform is enabled.
        /// Multiple platforms can be enabled simultaneously, and the system will perform load balancing among them.
        /// </summary>

        public bool ChatGptApiEnable { get; set; } = false;
        public bool GeminiApiEnable { get; set; } = false;
        public bool DeepSeekApiEnable { get; set; } = false;
        public bool LMLocalAIEnable { get; set; } = false;
        public bool DeepLApiEnable { get; set; } = false;

        #endregion

        #region ApiKey Set

        /// <summary>
        /// Stores API keys and model names for various translation and AI platforms.
        /// These keys must be obtained from the respective service providers.
        /// </summary>

        /// <summary>
        /// OpenAI ChatGPT API key.
        /// </summary>
        public List<string> ChatGptKey { get; set; } = new List<string>();

        /// <summary>
        /// Model name for ChatGPT (e.g., gpt-4o-mini).
        /// </summary>
        public string ChatGptModel { get; set; } = "gpt-4.1-nano";

        /// <summary>
        /// Google Gemini API key.
        /// </summary>
        public List<string> GeminiKey { get; set; } = new List<string>();

        /// <summary>
        /// Model name for Gemini (e.g., gemini-2.0-flash).
        /// </summary>
        public string GeminiModel { get; set; } = "gemini-2.5-flash";

        /// <summary>
        /// DeepSeek API key.
        /// </summary>
        public List<string> DeepSeekKey { get; set; } = new List<string>();

        /// <summary>
        /// Model name for DeepSeek (e.g., deepseek-chat).
        /// </summary>
        public string DeepSeekModel { get; set; } = "deepseek-chat";

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
        public string LMModel { get; set; } = "google/gemma-3-12b";

        #endregion

        #region EngineSetting

        /// <summary>
        /// The ratio of the maximum thread count at which throttling is triggered. 
        /// Range is 0 to 1, default is 0.5 meaning throttling starts when over 50% usage.
        /// </summary>
        public double ThrottleRatio { get; set; } = 0.7;

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
        /// Specifies the maximum number of context characters to include during generation.
        /// For example, if set to 200, the total character count of all context lines will not exceed 200.
        /// </summary>
        public int ContextLimit { get; set; } = 150;

        /// <summary>
        /// Allows retrieval of the entire database using only the source text.
        /// </summary>
        public bool EnableGlobalSearch { get; set; } = false;


        /// <summary>
        /// Protective symbols such as <(.*?)> are created and applied to the TranslationPreprocessor to prevent certain characters from being mistranslated.
        /// </summary>
        public List<string> ProtectedPatterns = new List<string>();

        /// <summary>
        /// Configure the maximum number of rejections,Used to prevent infinite loops.
        /// </summary>
        public int MaxTranslationAttempts = 20;

        /// <summary>
        /// Waiting time for retrying.
        /// </summary>
        public int ReTryWaitTime = 1000;

        #endregion

        /// <summary>
        /// User-defined custom prompt sent to the AI model.
        /// This prompt can be used to guide the AI's behavior or translation style.
        /// </summary>
        public string UserCustomAIPrompt { get; set; } = "";
    }

    public class EngineConfig
    {
        public static EngineConfigJson Config = new EngineConfigJson();

        public static void SyncTrdCount()
        {
            if (Config.AutoSetThreadLimit)
            {
                Config.MaxThreadCount = EngineConfig.AutoCalcThreadLimit();
            }
        }

        /// <summary>
        /// Automatically limit the number of concurrent threads
        /// </summary>
        /// <returns></returns>
        public static int AutoCalcThreadLimit()
        {
            int AutoThread = 0;

            AutoThread += Config.ChatGptApiEnable && !string.IsNullOrWhiteSpace(Config.ChatGptKey) ? 2 : 0;

            AutoThread += Config.GeminiApiEnable && !string.IsNullOrWhiteSpace(Config.GeminiKey) ? 2 : 0;

            AutoThread += Config.DeepSeekApiEnable && !string.IsNullOrWhiteSpace(Config.DeepSeekKey) ? 2 : 0;

            AutoThread += Config.LMLocalAIEnable ? 2 : 0;

            AutoThread += Config.DeepLApiEnable && !string.IsNullOrWhiteSpace(Config.DeepLKey) ? 2 : 0;

            if (AutoThread == 2)
            {
                if (Config.LMLocalAIEnable)
                {
                    try
                    {
                        AutoThread = Environment.ProcessorCount;
                        AutoThread = AutoThread / 2;

                        if (AutoThread <= 0)
                        {
                            AutoThread = Environment.ProcessorCount;
                        }
                    }
                    catch
                    {

                    }

                    if (AutoThread <= 0)
                    {
                        AutoThread = 3;
                    }

                }
            }

            return AutoThread;
        }

        private static readonly byte[] XorKey = Encoding.UTF8.GetBytes("PhoenixEngine");

        private static byte[] XOREncrypt(byte[] data)
        {
            byte[] result = new byte[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                result[i] = (byte)(data[i] ^ XorKey[i % XorKey.Length]);
            }
            return result;
        }

        private static byte[] XORDecrypt(byte[] data)
        {
            return XOREncrypt(data);
        }


     

        //Use Xor to easily encrypt and store user API keys to ensure security
        public static void Save()
        {
            string GetJson = JsonConvert.SerializeObject(Config);
            var EncryptedBytes = XOREncrypt(Encoding.UTF8.GetBytes(GetJson));
            File.WriteAllBytes(Engine.CurrentPath + "EngineConfig.data", EncryptedBytes);
        }

        public static void Load()
        {
            NextCall:
            string SetFullPath = Engine.CurrentPath + "EngineConfig.data";
            if (!File.Exists(SetFullPath))
            {
                Save();
                return;
            }

            try 
            { 
                var DecryptedBytes = XORDecrypt(File.ReadAllBytes(SetFullPath));
                EngineConfig.Config = JsonConvert.DeserializeObject<EngineConfigJson>(Encoding.UTF8.GetString(DecryptedBytes));
                EngineConfig.Config.LMModel = "(Auto)";
            }
            catch 
            {
                Save();
                goto NextCall;
            }
        }
    }
}
