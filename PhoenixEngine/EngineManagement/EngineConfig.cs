using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PhoenixEngine.DataBaseManagement;
using PhoenixEngine.TranslateCore;

namespace PhoenixEngine.EngineManagement
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

        #endregion


        public static bool PreTranslateEnable { get; set; } = true;


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
        public static bool LMLocalAIEnable { get; set; } = false;
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
        public static string LMModel { get; set; } = "google/gemma-3-12b";

        #endregion

        #region EngineSetting

        /// <summary>
        /// The ratio of the maximum thread count at which throttling is triggered. 
        /// Range is 0 to 1, default is 0.5 meaning throttling starts when over 50% usage.
        /// </summary>
        public static double ThrottleRatio { get; set; } = 0.7;

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

        /// <summary>
        /// Automatically limit the number of concurrent threads
        /// </summary>
        /// <returns></returns>
        public static int AutoCalcThreadLimit()
        {
            return 2;
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
            using (var Ms = new MemoryStream())
            using (var Writer = new BinaryWriter(Ms, Encoding.UTF8, true))
            {
                Writer.Write(ProxyIP ?? "");
                Writer.Write(GlobalRequestTimeOut);

                Writer.Write(DefPageSize);

                Writer.Write(PreTranslateEnable);

                Writer.Write(ChatGptApiEnable);
                Writer.Write(GeminiApiEnable);
                Writer.Write(CohereApiEnable);
                Writer.Write(DeepSeekApiEnable);
                Writer.Write(BaichuanApiEnable);
                Writer.Write(GoogleYunApiEnable);
                Writer.Write(LMLocalAIEnable);
                Writer.Write(DeepLApiEnable);

                Writer.Write(GoogleApiKey ?? "");
                Writer.Write(ChatGptKey ?? "");
                Writer.Write(ChatGptModel ?? "");
                Writer.Write(GeminiKey ?? "");
                Writer.Write(GeminiModel ?? "");
                Writer.Write(DeepSeekKey ?? "");
                Writer.Write(DeepSeekModel ?? "");
                Writer.Write(BaichuanKey ?? "");
                Writer.Write(BaichuanModel ?? "");
                Writer.Write(CohereKey ?? "");
                Writer.Write(DeepLKey ?? "");
                Writer.Write(IsFreeDeepL);

                Writer.Write(LMHost ?? "");
                Writer.Write(LMPort);
                Writer.Write(LMModel ?? "");

                Writer.Write(ThrottleRatio);
                Writer.Write(ThrottleDelayMs);
                Writer.Write(MaxThreadCount);
                Writer.Write(AutoSetThreadLimit);
                Writer.Write(ContextEnable);
                Writer.Write(ContextLimit);
                Writer.Write(UserCustomAIPrompt ?? "");

                Writer.Flush();

                var PlainBytes = Ms.ToArray();
                var EncryptedBytes = XOREncrypt(PlainBytes);
                File.WriteAllBytes(Engine.CurrentPath + "EngineConfig.data", EncryptedBytes);
            }
        }

        public static void Load()
        {
            string SetFullPath = Engine.CurrentPath + "EngineConfig.data";
            if (!File.Exists(SetFullPath))
            {
                Save();
                return;
            }

            try
            {

                var EncryptedBytes = File.ReadAllBytes(SetFullPath);
                var PlainBytes = XORDecrypt(EncryptedBytes);

                using (var Ms = new MemoryStream(PlainBytes))
                using (var Reader = new BinaryReader(Ms, Encoding.UTF8, true))
                {
                    ProxyIP = Reader.ReadString();
                    GlobalRequestTimeOut = Reader.ReadInt32();

                    DefPageSize = Reader.ReadInt32();
                    if (DefPageSize == 0)
                    {
                        DefPageSize = 20;
                    }

                    PreTranslateEnable = Reader.ReadBoolean();

                    ChatGptApiEnable = Reader.ReadBoolean();
                    GeminiApiEnable = Reader.ReadBoolean();
                    CohereApiEnable = Reader.ReadBoolean();
                    DeepSeekApiEnable = Reader.ReadBoolean();
                    BaichuanApiEnable = Reader.ReadBoolean();
                    GoogleYunApiEnable = Reader.ReadBoolean();
                    LMLocalAIEnable = Reader.ReadBoolean();
                    DeepLApiEnable = Reader.ReadBoolean();

                    GoogleApiKey = Reader.ReadString();
                    ChatGptKey = Reader.ReadString();
                    ChatGptModel = Reader.ReadString();
                    GeminiKey = Reader.ReadString();
                    GeminiModel = Reader.ReadString();
                    DeepSeekKey = Reader.ReadString();
                    DeepSeekModel = Reader.ReadString();
                    BaichuanKey = Reader.ReadString();
                    BaichuanModel = Reader.ReadString();
                    CohereKey = Reader.ReadString();
                    DeepLKey = Reader.ReadString();
                    IsFreeDeepL = Reader.ReadBoolean();

                    LMHost = Reader.ReadString();
                    LMPort = Reader.ReadInt32();
                    LMModel = Reader.ReadString();

                    ThrottleRatio = Reader.ReadDouble();
                    ThrottleDelayMs = Reader.ReadInt32();
                    MaxThreadCount = Reader.ReadInt32();
                    AutoSetThreadLimit = Reader.ReadBoolean();
                    ContextEnable = Reader.ReadBoolean();
                    ContextLimit = Reader.ReadInt32();
                    UserCustomAIPrompt = Reader.ReadString();
                }

            }
            catch
            {
                Save();
            }
        }

        #endregion
    }
}
