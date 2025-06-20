using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using PhoenixEngine.DataBaseManagement;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManagement;

namespace PhoenixEngine.EngineManagement
{
    public class Engine
    {
        /// <summary>
        /// Instance of the local SQLite database helper.
        /// Represents the pointer/reference to the current local database.
        /// </summary>
        public static SQLiteHelper LocalDB = new SQLiteHelper();

        public static void Init()
        {
            string GetFilePath = GetFullPath(@"\Engine.db");

            if (!File.Exists(GetFilePath))
            {
                SQLiteConnection.CreateFile(GetFilePath);
            }

            AdvancedDictionary.Init();
            CloudDBCache.Init();
            LocalDBCache.Init();

            LocalDB.OpenSql(GetFilePath);
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

    }
}
