using System;
using System.IO;
using System.Linq;
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
    public class Phoenix : ConfigExtend
    {
        //The engine allows the creation of multiple instances of the Translator, each capable of translating its own content, but sharing a single AIMemory. This improves context utilization.
        public static AITranslationMemory AIMemory = new AITranslationMemory();
        public static string Version = "3.2.8.3";

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

        public static string PluginsPath = @"\CorePlugins\";
        private static string CurrentPath = "";
        public static void Init(string StartupPath,Action<int>StepAction = null)
        {
            CurrentPath = StartupPath;

            string GetFilePath = GetFullPath(@"\Engine.db");

            if (!File.Exists(GetFilePath))
            {
                StepAction?.Invoke(1);
                P_SQLite.CreateDataBase(GetFilePath);
            }

            LocalDB.OpenSQL(GetFilePath);

            StepAction?.Invoke(2);
            AdvancedDictionary.Init();

            StepAction?.Invoke(3);
            CloudDBCache.Init();
            LocalDBCache.Init();
            FontColorFinder.Init();

            StepAction?.Invoke(5);
            ChineseVariantMap.Init();

            StepAction?.Invoke(6);
            UniqueKeyHelper.Init();

            StepAction?.Invoke(7);
            Phoenix.LoadConfig();

            StepAction?.Invoke(8);
            ProxyCenter.UsingProxy();

            StepAction?.Invoke(9);
            WordAutoComplete.DatabaseDirectory = GetFullPath(PluginsPath);
            WordAutoComplete.Init();

            StepAction?.Invoke(10);
            ReSetKeyData();
        }

        public static void Vacuum()
        {
            LocalDB.ExecuteNonQuery("vacuum");
        }

        public static string GetFullPath(string Path)
        {
            string GetShellPath = CurrentPath;

            if (Path.StartsWith(@"\"))
            {
                Path = Path.Substring(1);
            }

            return GetShellPath + Path;
        }
        public static void ReSetKeyData()
        {
            KeyData = new KeyManage();
            KeyData.Init();
        }
    }
}
