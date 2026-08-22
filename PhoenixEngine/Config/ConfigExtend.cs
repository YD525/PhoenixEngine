using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using PhoenixEngine.Common;
using PhoenixEngine.Platform;
using PhoenixEngine.Translate;

namespace PhoenixEngine
{
    public class CustomPlatformInFo
    {
        public string Name = "";
        public int CustomID = 0;

        public string Url = "";
        public List<ReqReplaceTag> Url_Tags = new List<ReqReplaceTag>();

        public string PayLoad = "";
        public ReqEncodeType PayLoadEncode = ReqEncodeType.Null;
        public List<ReqReplaceTag> PayLoad_Tags = new List<ReqReplaceTag>();

        public string Header = "";
        public List<ReqReplaceTag> Header_Tags = new List<ReqReplaceTag>();

        public bool IsPost = true;

        public SignEncode Sign = SignEncode.Null;
       
        public string SignParams = "";

        public bool SignToLower = false;

        public CustomPlatformType Type = CustomPlatformType.Null;

        public ReqQueryRuleItem QueryRule = new ReqQueryRuleItem();
    }
    public class PlatformConfig
    {
        public List<string> ApiKeys { get; set; } = new List<string>();
        public PlatformType Platform { get; set; } = PlatformType.Null;

        public bool Enable = false;
        public string Model { get; set; } = "";
        public CustomPlatformInFo CustomInFo { get; set; } = null;

        public int LocalPort = 0;

        public bool IsFree = false;

        public PlatformConfig()
        {
        }
        public PlatformConfig(PlatformType Type)
        {
            this.Platform = Type;
        }
    }
    public class EngineConfigJson
    {
        #region Init

        public int Init = 0;

        #endregion
        #region RequestConfig

        /// <summary>
        /// Configured http proxy or local proxy for network requests.
        /// </summary>
        public string ProxyUrl { get; set; } = "";

        public string ProxyUserName { get; set; } = "";

        public string ProxyPassword { get; set; } = "";

        #endregion

        #region DataBase

        /// <summary>
        /// Default page size for pagination.  
        /// Represents how many items are shown per page by default.
        /// </summary>
        public int DefPageSize { get; set; } = 50;

        #endregion


        public bool PreTranslateEnable { get; set; } = true;


        #region Platform Cls


        #endregion

        #region ApiKey Set

        public Dictionary<int, PlatformConfig> PlatformConfigs { get; set; } = null;

        #endregion

        #region EngineSetting

        /// <summary>
        /// The ratio of the maximum thread count at which throttling is triggered. 
        /// Range is 0 to 1, default is 0.7 meaning throttling starts when over 70% usage.
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
        public bool AutoSetThreadLimit { get; set; } = false;

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
        public int ContextLimit { get; set; } = 200;

        /// <summary>
        /// Determine the size of each bucket.
        /// </summary>
        public int BucketLengthLimit { get; set; } = 3900;

        /// <summary>
        /// Always send the complete conversation context to the AI, including already translated lines, to ensure translation consistency and preserve the original dialogue flow.
        /// </summary>
        public bool PreserveConversationContext { get; set; } = false;


        /// <summary>
        /// Enable duplicate removal in confirmed dialogue relationships. Similarity-based groups are always deduplicated, while this option also removes duplicates from fully related dialogue contexts. This can reduce AI token usage but may affect dialogue context completeness.
        /// </summary>
        public bool ForceContextDeduplication { get; set; } = false;

        /// <summary>
        /// Whether to enforce absolute purity of "Link Buckets" (Type == 1 — buckets holding
        /// units that have a real, established link relationship in the ESP file).
        ///
        /// Enabled (true): once built, Link Buckets can never have additional content appended
        /// or merged into them (orphan-unit backfill, similarity-bucket merging, etc. all skip
        /// Link Buckets). Every unit inside a Link Bucket is guaranteed to come from the same
        /// genuine ESP link chain — nothing unrelated gets mixed in. This matters because these
        /// buckets are sent together as shared context; letting unrelated content leak in can
        /// confuse the AI about which entries are actually related, leading to wrong context
        /// inference or inconsistent translations.
        ///
        /// Disabled (false, default): Link Buckets may still receive extra content appended to
        /// them whenever there's remaining capacity, which improves packing efficiency and
        /// reduces the total bucket count, at the cost of allowing unrelated entries into an
        /// otherwise link-only bucket.
        /// </summary>
        public bool StrictLinkBucketPurity { get; set; } = false;

        /// <summary>
        /// The system decides to completely disconnect a node after it fails a few times, and then restore the connection when the next batch translation begins.
        /// </summary>
        public int FailThreshold { get; set; } = 10;

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
        public int MaxTranslationAttempts = 3;

        /// <summary>
        /// Waiting time for retrying.
        /// </summary>
        public int ReTryWaitTime = 1000;//ms

        #endregion

        /// <summary>
        /// User-defined custom prompt sent to the AI model.
        /// This prompt can be used to guide the AI's behavior or translation style.
        /// </summary>
        public string UserCustomAIPrompt { get; set; } = "";

        public PlatformConfig GetPlatformData(PlatformType Type)
        {
            lock (Phoenix.QueryPlatformDataLock)
            {
                return this.PlatformConfigs[(int)Type];
            }
        }

        public PlatformConfig GetPlatformData(int CustomID)
        {
            lock (Phoenix.QueryPlatformDataLock)
            {
                return this.PlatformConfigs[CustomID];
            }
        }

        public string GetPlatformKeysStr(PlatformConfig Config)
        {
            lock (Phoenix.QueryPlatformDataLock)
            {
                string KeysStr = "";

                for (int i = 0; i < Config.ApiKeys.Count; i++)
                {
                    KeysStr += Config.ApiKeys[i] + ";\n";
                }

                return KeysStr;
            }
        }

        public List<string> KeysStrToArray(string KeysStr)
        {
            KeysStr = KeysStr.Replace("\r\n", "");
            KeysStr = KeysStr.Replace("\n", "");

            List<string> Keys = new List<string>();
            foreach (var GetLine in KeysStr.Split(';'))
            {
                if (GetLine.Trim().Length > 0)
                {
                    Keys.Add(GetLine.Trim());
                }
            }
            return Keys;
        }
    }

    public abstract class ConfigExtend
    {
        public static EngineConfigJson Config = new EngineConfigJson();

        private static readonly byte[] XorKey = Encoding.UTF8.GetBytes("PhoenixEngine");

        public static void SetDefaultModel()
        {
            if (Config.PlatformConfigs == null)
            {
                Config.PlatformConfigs = new Dictionary<int, PlatformConfig>();

                Config.PlatformConfigs.Add((int)PlatformType.ChatGpt, new PlatformConfig(PlatformType.ChatGpt));
                Config.PlatformConfigs.Add((int)PlatformType.Gemini, new PlatformConfig(PlatformType.Gemini));
                Config.PlatformConfigs.Add((int)PlatformType.LMLocalAI, new PlatformConfig(PlatformType.LMLocalAI));
                Config.PlatformConfigs.Add((int)PlatformType.DeepSeek, new PlatformConfig(PlatformType.DeepSeek));
                Config.PlatformConfigs.Add((int)PlatformType.DeepL, new PlatformConfig(PlatformType.DeepL));

                Config.PlatformConfigs.Add((int)PlatformType.HumanTranslation, new PlatformConfig(PlatformType.HumanTranslation));

                if (Config.PlatformConfigs[(int)PlatformType.LMLocalAI].LocalPort == 0)
                {
                    Config.PlatformConfigs[(int)PlatformType.LMLocalAI].LocalPort = 1234;
                }
                if (Config.PlatformConfigs[(int)PlatformType.ChatGpt].Model == "")
                {
                    Config.PlatformConfigs[(int)PlatformType.ChatGpt].Model = "gpt-4.1-nano";
                }
                if (Config.PlatformConfigs[(int)PlatformType.Gemini].Model == "")
                {
                    Config.PlatformConfigs[(int)PlatformType.Gemini].Model = "gemini-2.5-flash";
                }
                if (Config.PlatformConfigs[(int)PlatformType.DeepSeek].Model == "")
                {
                    Config.PlatformConfigs[(int)PlatformType.DeepSeek].Model = "deepseek-v4-pro";
                }

                Config.PlatformConfigs[(int)PlatformType.DeepL].IsFree = true;

                if (Config.Init == 0)
                {
                    int EnableCount = 0;
                    foreach (var GetItem in new Dictionary<int, PlatformConfig>(Config.PlatformConfigs))
                    {
                        if (GetItem.Value.Enable && GetItem.Value.Platform!= PlatformType.HumanTranslation)
                        { 
                           EnableCount++;
                        }
                    }

                    if (EnableCount == 0)
                    {
                        Config.PlatformConfigs[(int)PlatformType.HumanTranslation].Enable = true;
                    }
                    Config.Init = 1;
                }
            }
        }
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
        public static void SaveConfig()
        {
            string GetJson = JsonConvert.SerializeObject(Config);
            var EncryptedBytes = XOREncrypt(Encoding.UTF8.GetBytes(GetJson));
            File.WriteAllBytes(Phoenix.GetFullPath("EngineConfig.data"), EncryptedBytes);
        }

        public static bool CheckAvailableNodes()
        {
            int EnableCount = 0;
            for (int i = 0; i < Phoenix.Config.PlatformConfigs.Count; i++)
            { 
                var GetKey = Phoenix.Config.PlatformConfigs.ElementAt(i).Key;
                if (Phoenix.Config.PlatformConfigs[GetKey].ApiKeys.Count > 0)
                {
                    if (Phoenix.Config.PlatformConfigs[GetKey].Enable)
                    {
                        EnableCount++;
                    }
                }
                else
                if (Phoenix.Config.PlatformConfigs[GetKey].Platform == PlatformType.LMLocalAI && Phoenix.Config.PlatformConfigs[GetKey].Enable)
                {
                    EnableCount++;
                }
                else
                if (Phoenix.Config.PlatformConfigs[GetKey].Platform == PlatformType.HumanTranslation && Phoenix.Config.PlatformConfigs[GetKey].Enable)
                {
                    EnableCount++;
                }
            }
            if (EnableCount > 0)
            {
                return true;
            }
            return false;
        }
        public static void LoadConfig()
        {
            NextCall:
            string SetFullPath = Phoenix.GetFullPath("EngineConfig.data");
            if (!File.Exists(SetFullPath))
            {
                SetDefaultModel();
                SaveConfig();
                return;
            }
            else
            {
                try
                {
                    if (new FileInfo(SetFullPath).Length > JsonPayload.MaximumDocumentBytes)
                        throw new InvalidDataException("The engine configuration exceeds the JSON size limit.");
                    var DecryptedBytes = XORDecrypt(File.ReadAllBytes(SetFullPath));
                    EngineConfigJson loadedConfig;
                    if (!JsonPayload.TryDeserialize(
                        Encoding.UTF8.GetString(DecryptedBytes),
                        value => value != null,
                        out loadedConfig))
                    {
                        throw new InvalidDataException("The engine configuration is invalid.");
                    }
                    Config = loadedConfig;

                    SetDefaultModel();
                }
                catch
                {
                    SaveConfig();
                    goto NextCall;
                }
            }
        }
    }
}
