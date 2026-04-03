using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PhoenixEngine.Additional;
using PhoenixEngine.ADO;
using PhoenixEngine.Engine;
using PhoenixEngine.Engine.ADO;
using PhoenixEngine.Language;
using PhoenixEngine.Memory;
using PhoenixEngine.Platform;
using PhoenixEngine.Request;
using PhoenixEngine.Translate;

namespace PhoenixEngine
{
    public class ThreadUsageInfo
    {
        public int CurrentThreads { get; set; } = 0;
        public int MaxThreads { get; set; } = 0;
    }

    public class Phoenix : ConfigExtend
    {
        public static AITranslationMemory AIMemory = new AITranslationMemory();

        public static string Version = "3.1.1.8";
        public static string CurrentPath = "";

        public static object QueryPlatformDataLock = new object();

        /// <summary>
        /// Instance of the local SQLite database helper.
        /// Represents the pointer/reference to the current local database.
        /// </summary>
        public static P_SQLite LocalDB = new P_SQLite();
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
        public static string GetVersion()
        {
            return Phoenix.Version;
        }
        public static void Init()
        {
            CurrentPath = GetFullPath(@"\");

            string GetFilePath = GetFullPath(@"\Engine.db");

            if (!File.Exists(GetFilePath))
            {
                P_SQLite.CreateDataBase(GetFilePath);
            }

            LocalDB.OpenSQL(GetFilePath);

            AdvancedDictionary.Init();

            CloudDBCache.Init();
            LocalDBCache.Init();
            FontColorFinder.Init();

            ChineseVariantMap.Init();

            UniqueKeyHelper.Init();

            Phoenix.LoadConfig();
            ProxyCenter.UsingProxy();

            WordAutoComplete.DatabaseDirectory = GetFullPath(@"\wordfreq\");
            WordAutoComplete.Init();

            ReSetKeyData();
        }

        public static void Vacuum()
        {
            LocalDB.ExecuteNonQuery("vacuum");
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
        public static void ReSetKeyData()
        {
            KeyData = new KeyManage();
            KeyData.Init();
        }

        public static void AddAIMemory(Translator TranslatorRef, string Original, string Translated)
        {
            Phoenix.AIMemory.AddTranslation(TranslatorRef.From, TranslatorRef.To, Original, Translated);
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
