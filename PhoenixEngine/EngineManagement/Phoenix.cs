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

    public class Phoenix : ConfigExtend
    {
        public static string Version = "1.2.3.5";
        public static string CurrentPath = "";

        public static Translator Instance = null;

        public static object QueryPlatformDataLock = new object();

        /// <summary>
        /// Instance of the local SQLite database helper.
        /// Represents the pointer/reference to the current local database.
        /// </summary>
        public static SQLiteHelper LocalDB = new SQLiteHelper();
        public static KeyManage KeyData = new KeyManage();

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
                else
                if (GetConfig.Platform == PlatformType.LMLocalAI)
                {
                    AutoThread++;
                }
            }

            return AutoThread;
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

            Instance = new Translator(Phoenix.From,Phoenix.To);

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

        public static Languages From = Languages.English;

        public static Languages To = Languages.English;

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
