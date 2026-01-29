using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using PhoenixEngine.ConvertManager;
using PhoenixEngine.DataBaseManagement;
using PhoenixEngine.PlatformManagement;
using PhoenixEngine.RequestManagement;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManage;
using PhoenixEngine.TranslateManagement;
using static PhoenixEngine.TranslateManage.TransCore;

namespace PhoenixEngine.EngineManagement
{
    public class ThreadUsageInfo
    {
        public int CurrentThreads { get; set; } = 0;
        public int MaxThreads { get; set; } = 0;
    }
    public class CustomPlatformInFo
    {
        public string Name = "";
        public int CustomID = 0;

        public string Url = "";
        public List<CReplaceTag> Url_Tags = new List<CReplaceTag>();

        public string PayLoad = "";
        public CEncodeType PayLoadEncode = CEncodeType.Null;
        public List<CReplaceTag> PayLoad_Tags = new List<CReplaceTag>();

        public string Header = "";
        public List<CReplaceTag> Header_Tags = new List<CReplaceTag>();

        public CustomPlatformType Type = CustomPlatformType.Null;
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


        #region Platform Cls


        #endregion

        #region ApiKey Set

        public Dictionary<int, PlatformConfig> PlatformConfigs { get; set; } = null;

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

    public class Phoenix
    {
        public static string Version = "1.2.3.2";
        public static string CurrentPath = "";

        public static object QueryPlatformDataLock = new object();

        public static EngineConfigJson Config = new EngineConfigJson();
        /// <summary>
        /// Instance of the local SQLite database helper.
        /// Represents the pointer/reference to the current local database.
        /// </summary>
        public static SQLiteHelper LocalDB = new SQLiteHelper();
        public static KeyManage KeyData = new KeyManage();

        public static void SetDefaultModel()
        {
            if (Config.PlatformConfigs == null)
            {
                Config.PlatformConfigs = new Dictionary<int, PlatformConfig>();

                Config.PlatformConfigs.Add((int)PlatformType.ChatGpt,new PlatformConfig(PlatformType.ChatGpt));
                Config.PlatformConfigs.Add((int)PlatformType.Gemini, new PlatformConfig(PlatformType.Gemini));
                Config.PlatformConfigs.Add((int)PlatformType.LMLocalAI, new PlatformConfig(PlatformType.LMLocalAI));
                Config.PlatformConfigs.Add((int)PlatformType.DeepSeek, new PlatformConfig(PlatformType.DeepSeek));
                Config.PlatformConfigs.Add((int)PlatformType.DeepL, new PlatformConfig(PlatformType.DeepL));

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
                    Config.PlatformConfigs[(int)PlatformType.DeepSeek].Model = "deepseek-chat";
                }

                Config.PlatformConfigs[(int)PlatformType.DeepL].IsFree = true;
            }
        }

        public static void SyncTrdCount()
        {
            if (Config.AutoSetThreadLimit)
            {
                Config.MaxThreadCount = Phoenix.AutoCalcThreadLimit();
            }
        }

        /// <summary>
        /// Automatically limit the number of concurrent threads
        /// </summary>
        /// <returns></returns>
        public static int AutoCalcThreadLimit()
        {
            int AutoThread = 0;

            for (int i = 0; i < Phoenix.Config.PlatformConfigs.Count; i++)
            { 
                int GetKey = Phoenix.Config.PlatformConfigs.ElementAt(i).Key;
                var GetConfig = Phoenix.Config.PlatformConfigs[GetKey];
                if (GetConfig.ApiKeys.Count > 0 && GetConfig.Enable)
                {
                    AutoThread++;
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
        public static void SaveConfig()
        {
            string GetJson = JsonConvert.SerializeObject(Config);
            var EncryptedBytes = XOREncrypt(Encoding.UTF8.GetBytes(GetJson));
            File.WriteAllBytes(Phoenix.CurrentPath + "EngineConfig.data", EncryptedBytes);
        }

        public static void LoadConfig()
        {
            NextCall:
            string SetFullPath = Phoenix.CurrentPath + "EngineConfig.data";
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
                    var DecryptedBytes = XORDecrypt(File.ReadAllBytes(SetFullPath));
                    Phoenix.Config = JsonConvert.DeserializeObject<EngineConfigJson>(Encoding.UTF8.GetString(DecryptedBytes));

                    SetDefaultModel();
                }
                catch
                {
                    SaveConfig();
                    goto NextCall;
                }
            }  
        }

        public static void Init()
        {
            CurrentPath = GetFullPath(@"\");

            string GetFilePath = GetFullPath(@"\Engine.db");

            if (!File.Exists(GetFilePath))
            {
                SQLiteConnection.CreateFile(GetFilePath);
            }

            LocalDB.OpenSql(GetFilePath);

            AdvancedDictionary.Init();

            CloudDBCache.Init();
            LocalDBCache.Init();
            FontColorFinder.Init();

            UniqueKeyHelper.Init();

            Phoenix.LoadConfig();
            ProxyCenter.UsingProxy();

            ReSetKeyData();
        }

        public static void Vacuum()
        {
            LocalDB.ExecuteNonQuery("vacuum");
        }

        public static string LastLoadFileName = "";

        public static void LoadFile(string FilePath, bool CanSkipFuzzyMatching = false)
        {
            UniqueKeyItem NewKey = new UniqueKeyItem();
            var UniqueKey = UniqueKeyHelper.AddItemByReturn(ref NewKey, FilePath, CanSkipFuzzyMatching);
            LastLoadFileName = NewKey.FileName;

            ChangeUniqueKey(UniqueKey);
        }

        public static string GetFullPath(string Path)
        {
            string GetShellPath = System.AppContext.BaseDirectory;
            if (GetShellPath.EndsWith(@"\"))
            {
                if (Path.StartsWith(@"\"))
                {
                    Path = Path.Substring(1);
                }
            }
            return GetShellPath + Path;
        }

        private static BatchTranslationCore TranslationCore = null;


        public static Languages From = Languages.Auto;

        public static Languages To = Languages.Null;

        public static bool ConfigLanguage(Languages SetFrom, Languages SetTo)
        {
            if (SetFrom != Languages.Null && SetTo != Languages.Null)
            {
                Phoenix.From = SetFrom;
                Phoenix.To = SetTo;
                return true;
            }
            return false;
        }

        private static int FileUniqueKey = 0;

        public static void ChangeUniqueKey(int Rowid)
        {
            FileUniqueKey = Rowid;
            GetTranslatedCount(FileUniqueKey);
        }

        public static int TranslatedCount = 0;
        public static int GetTranslatedCount(int FileUniqueKey)
        {
            if (LastLoadFileName.Length == 0) return 0;
            string SqlOrder = $@"SELECT COUNT(*) AS TotalCount
FROM (
    SELECT Key
    FROM LocalTranslation
    WHERE FileUniqueKey = '{FileUniqueKey}' And [To] = '{(int)Phoenix.To}'
    
    UNION  
    SELECT Key
    FROM CloudTranslation
    WHERE FileUniqueKey = '{FileUniqueKey}' And [To] = '{(int)Phoenix.To}'
) AS Combined;";

            int GetCount = ConvertHelper.ObjToInt(Phoenix.LocalDB.ExecuteScalar(SqlOrder));

            TranslatedCount = GetCount;

            return GetCount;
        }
        public static int GetFileUniqueKey()
        {
            return Phoenix.FileUniqueKey;
        }

        public static void SkipWordAnalysis(bool Check)
        {
            if (TranslationCore != null)
            {
                TranslationCore.SkipWordAnalysis = Check;
            }
        }

        public static void Start()
        {
            Start(false);
        }

        public static void ReSetKeyData()
        {
            KeyData = new KeyManage();
            KeyData.Init();
        }

        public static void Start(bool ClearCache)
        {
            ReSetKeyData();

            if (From != Languages.Null && To != Languages.Null)
            {
                if (TranslationCore == null)
                {
                    TranslationCore = new BatchTranslationCore(Phoenix.From, Phoenix.To, new List<TranslationUnit>() { }, ClearCache);
                }

                TranslationCore.Start();
            }
        }

        public static void Stop()
        {
            if (TranslationCore != null)
            {
                TranslationCore.Stop();
            }
        }

        public static void End()
        {
            if (TranslationCore != null)
            {
                TranslationCore.Close();
            }
        }

        public static int GetThreadCount()
        {
            if (TranslationCore != null)
            {
                return TranslationCore.ThreadUsage.CurrentThreads;
            }

            return 0;
        }

        private static object AddTranslationUnitLocker = new object();
        public static int AddTranslationUnit(TranslationUnit Item, bool IsLeader = false)
        {
            if (TranslationCore == null)
            {
                return -1;
            }

            lock (AddTranslationUnitLocker)
            {
                return TranslationCore.AddWaitTransUnit(Item, IsLeader);
            }
        }
        public static TranslationUnit DequeueTranslated(ref bool IsEnd)
        {
            if (TranslationCore != null)
            {
                var GetItem = TranslationCore.DequeueTranslated(out bool TranslationEnd);
                IsEnd = TranslationEnd;

                return GetItem;
            }
            else
            {
                IsEnd = true;
            }

            return null;
        }

        public static void InitTranslationCore(Languages From, Languages To)
        {
            TranslationCore = new BatchTranslationCore(From, To, new List<TranslationUnit>() { });
        }
        public static void ClearUnits()
        {
            if (TranslationCore != null)
            {
                TranslationCore.UnitsToTranslate.Clear();
            }
        }
        public static int GetUnitCount()
        {
            if (TranslationCore != null)
            {
                return TranslationCore.UnitsToTranslate.Count;
            }

            return -1;
        }

        public static void AddAIMemory(string Original, string Translated)
        {
            EngineSelect.AIMemory.AddTranslation(Phoenix.From, Phoenix.To, Original, Translated);
        }

        public static string AppendDollarWrappedReplacements(string input)
        {
            // Create a regex to match text wrapped in $$...$$
            Regex OneRegex = new Regex(@"\$\$(.+?)\$\$");

            // Replace each match with {content}
            string Replaced = OneRegex.Replace(input, match => "{" + match.Groups[1].Value + "}");

            // Return the processed text only (original text is not preserved)
            return Replaced;
        }
    }
}
