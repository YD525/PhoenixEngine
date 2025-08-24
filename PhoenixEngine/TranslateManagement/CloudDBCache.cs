using System.Data;
using PhoenixEngine.ConvertManager;
using PhoenixEngine.EngineManagement;

namespace PhoenixEngine.TranslateCore
{
    // Copyright (c) 2025 YD525
    // Licensed under the MIT License.
    // See LICENSE file in the project root for full license information.
    //https://github.com/YD525/PhoenixEngine

    public class CloudDBCache
    {
        public static void Init()
        {
            string CheckTableSql = "SELECT name FROM sqlite_master WHERE type='table' AND name='CloudTranslation';";
            var Result = Engine.LocalDB.ExecuteScalar(CheckTableSql);

            if (Result == null || Result == DBNull.Value)
            {
                string CreateTableSql = @"
CREATE TABLE [CloudTranslation](
  [ModName] TEXT, 
  [Key] TEXT, 
  [To] INT, 
  [Result] TEXT
);";
                Engine.LocalDB.ExecuteNonQuery(CreateTableSql);
            }
        }

        public static bool DeleteCache(string ModName,string Key,Languages TargetLanguage)
        {
            try
            {
                string SqlOrder = "Delete From CloudTranslation Where [ModName] = '{0}' And [Key] = '{1}' And [To] = {2}";

                int State = Engine.LocalDB.ExecuteNonQuery(string.Format(SqlOrder, ModName, Key,(int)TargetLanguage));

                if (State!=0)
                {
                    return true;
                }

                return false;
            }
            catch { return false; }
        }
        public static string FindCache(string ModName,string Key, Languages TargetLanguage)
        {
            try { 
            string SqlOrder = "Select Result From CloudTranslation Where [ModName] = '{0}' And [Key] = '{1}' And [To] = {2}";

            string GetResult = ConvertHelper.ObjToStr(Engine.LocalDB.ExecuteScalar(string.Format(SqlOrder, ModName, Key,(int)TargetLanguage)));

            if (GetResult.Trim().Length > 0)
            {
                return System.Web.HttpUtility.HtmlDecode(GetResult);
            }

            return string.Empty;
            }
            catch { return string.Empty; }
        }

        public static bool AddCache(string ModName, string Key, int To,string Result)
        {
            if (ModName.Trim().Length == 0)
            {
               return false;
            }
            try {
            int GetRowID = ConvertHelper.ObjToInt(Engine.LocalDB.ExecuteScalar(String.Format("Select Rowid From CloudTranslation Where [ModName] = '{0}' And [Key] = '{1}' And [To] = {2}",ModName,Key,To)));

            if (GetRowID < 0)
            {
                string SqlOrder = "Insert Into CloudTranslation([ModName],[Key],[To],[Result])Values('{0}','{1}',{2},'{3}')";

                int State = Engine.LocalDB.ExecuteNonQuery(string.Format(SqlOrder,ModName,Key, To, System.Web.HttpUtility.HtmlEncode(Result)));

                if (State != 0)
                {
                    return true;
                }

                return false;
            }

            return false;
            }
            catch { return false; }
        }


        public static string FindCacheAndID(string ModName,string Key, int To,ref int ID)
        {
            try { 
            string SqlOrder = "Select Rowid,Result From CloudTranslation Where [ModName] = '{0}' And [Key] = '{1}' And [To] = {2}";

            DataTable GetResult = Engine.LocalDB.ExecuteQuery(string.Format(SqlOrder,ModName,Key,To));

            if (GetResult.Rows.Count > 0)
            {
                string GetStr = System.Web.HttpUtility.HtmlDecode(ConvertHelper.ObjToStr(GetResult.Rows[0]["Result"]));
                ID = ConvertHelper.ObjToInt(GetResult.Rows[0]["Rowid"]);
                return GetStr;
            }

            return string.Empty;
            }
            catch {return string.Empty; }
        }

        public static bool DeleteCacheByID(int Rowid)
        {
            try {
            string SqlOrder = "Delete From CloudTranslation Where Rowid = {0}";
            int State = Engine.LocalDB.ExecuteNonQuery(string.Format(SqlOrder,Rowid));
            if (State != 0)
            {
                return true;
            }
            return false;
            }
            catch { return false; }
        }

        public static bool ClearCloudCache(string ModName)
        {
            string SqlOrder = "Delete From CloudTranslation Where ModName = '" + ModName + "'";
            int State = Engine.LocalDB.ExecuteNonQuery(SqlOrder);
            if (State != 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
