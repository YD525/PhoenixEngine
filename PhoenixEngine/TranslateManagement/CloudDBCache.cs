using System.Data;
using PhoenixEngine.ConvertManager;
using PhoenixEngine.DataBaseManagement;
using PhoenixEngine.EngineManagement;

namespace PhoenixEngine.TranslateCore
{
    // Copyright (c) 2025 YD525
    // Licensed under the MIT License.
    // See LICENSE file in the project root for full license information.
    //https://github.com/YD525/PhoenixEngine

    public class CloudTranslationItem
    {
        public int FileUniqueKey = 0;
        public string Key = "";
        public int To = 0;
        public string Source = "";
        public string Result = "";

        public CloudTranslationItem(object FileUniqueKey, object Key, object To, object Source, object Result)
        {
            this.FileUniqueKey = ConvertHelper.ObjToInt(FileUniqueKey);
            this.Key = ConvertHelper.ObjToStr(Key);
            this.To = ConvertHelper.ObjToInt(To);
            this.Source = ConvertHelper.ObjToStr(Source);
            this.Result = ConvertHelper.ObjToStr(Result);
        }
    }

    public class CloudDBCache
    {
        public static void Init()
        {
            string TableName = "CloudTranslation";
            string CreateSql = @"
CREATE TABLE [CloudTranslation](
  [FileUniqueKey] INT, 
  [Key] TEXT, 
  [To] INT, 
  [Source] TEXT,
  [Result] TEXT
);";

            // Check if table exists
            string CheckTableSql = $"SELECT name FROM sqlite_master WHERE type='table' AND name='{TableName}';";
            var Result = Engine.LocalDB.ExecuteScalar(CheckTableSql);

            if (Result != null && Result != DBNull.Value)
            {
                // Table exists, check structure
                List<Dictionary<string, object>> Columns = Engine.LocalDB.ExecuteQuery($"PRAGMA table_info({TableName});");
                var ExistingCols = new HashSet<string>(
                    Columns.Select(R => R["name"].ToString()),
                    StringComparer.OrdinalIgnoreCase
                );

                string[] ExpectedCols = { "FileUniqueKey", "Key", "To", "Source", "Result" };
                bool StructureChanged =
                    ExistingCols.Count != ExpectedCols.Length ||
                    ExpectedCols.Any(C => !ExistingCols.Contains(C));

                if (StructureChanged)
                {
                    Engine.LocalDB.ExecuteNonQuery($"DROP TABLE IF EXISTS [{TableName}];");
                    Engine.LocalDB.ExecuteNonQuery(CreateSql);
                }
            }
            else
            {
                // Create if not exists
                Engine.LocalDB.ExecuteNonQuery(CreateSql);
            }
        }

        public static bool DeleteCache(int FileUniqueKey, string Key, Languages TargetLanguage)
        {
            try
            {
                string SqlOrder = "Delete From CloudTranslation Where [FileUniqueKey] = {0} And [Key] = '{1}' And [To] = {2}";

                int State = Engine.LocalDB.ExecuteNonQuery(string.Format(SqlOrder, FileUniqueKey, Key, (int)TargetLanguage));

                if (State != 0)
                {
                    return true;
                }

                return false;
            }
            catch { return false; }
        }
        public static string FindCache(int FileUniqueKey, string Key, Languages TargetLanguage)
        {
            try
            {
                string SqlOrder = "Select Result From CloudTranslation Where [FileUniqueKey] = '{0}' And [Key] = '{1}' And [To] = {2}";

                string GetResult = ConvertHelper.ObjToStr(Engine.LocalDB.ExecuteScalar(string.Format(SqlOrder, FileUniqueKey, Key, (int)TargetLanguage)));

                if (GetResult.Trim().Length > 0)
                {
                    return SqlSafeCodec.Decode(GetResult);
                }

                return string.Empty;
            }
            catch { return string.Empty; }
        }

        public static bool AddCache(int FileUniqueKey, string Key, int To, string Source, string Result)
        {
            try
            {

                int GetRowID = ConvertHelper.ObjToInt(Engine.LocalDB.ExecuteScalar(String.Format("Select Rowid From CloudTranslation Where [FileUniqueKey] = {0} And [Key] = '{1}' And [To] = {2}", FileUniqueKey, Key, To)));

                if (GetRowID <= 0)
                {
                    string SqlOrder = "Insert Into CloudTranslation([FileUniqueKey],[Key],[To],[Source],[Result])Values({0},'{1}',{2},'{3}','{4}')";

                    int State = Engine.LocalDB.ExecuteNonQuery(string.Format(SqlOrder, FileUniqueKey, Key, To, SqlSafeCodec.Encode(Source), SqlSafeCodec.Encode(Result)));

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

        public static List<CloudTranslationItem> MatchCloudItem(int To, string Source, int Limit = 5)
        {
            try
            {
                List<CloudTranslationItem> CloudTranslationItems = new List<CloudTranslationItem>();

                string SqlOrder = "Select * From CloudTranslation Where [To] = {0} And [Source] = '{1}' Limit 5";
                List<Dictionary<string, object>> NTable = Engine.LocalDB.ExecuteQuery(string.Format(SqlOrder, To, SqlSafeCodec.Encode(Source)));
                if (NTable.Count > 0)
                {
                    for (int i = 0; i < NTable.Count; i++)
                    {
                        var Row = NTable[i]; 

                        CloudTranslationItems.Add(new CloudTranslationItem(
                            Row["FileUniqueKey"],
                            Row["Key"],
                            Row["To"],
                            SqlSafeCodec.Decode(ConvertHelper.ObjToStr(Row["Source"])),
                            SqlSafeCodec.Decode(ConvertHelper.ObjToStr(Row["Result"]))
                        ));
                    }
                }

                return CloudTranslationItems;
            }
            catch { }

            return new List<CloudTranslationItem>();
        }

        public static List<CloudTranslationItem> MatchOtherCloudItem(int Rowid,int To, string Source, int Limit = 5)
        {
            try
            {
                List<CloudTranslationItem> CloudTranslationItems = new List<CloudTranslationItem>();

                string SqlOrder = "Select * From CloudTranslation Where [To] = {0} And [Source] = '{1}' And Rowid != {2} Limit 5";
                List<Dictionary<string, object>> NTable = Engine.LocalDB.ExecuteQuery(string.Format(SqlOrder, To, SqlSafeCodec.Encode(Source),Rowid));
                if (NTable.Count > 0)
                {
                    for (int i = 0; i < NTable.Count; i++)
                    {
                        var Row = NTable[i];

                        CloudTranslationItems.Add(new CloudTranslationItem(
                            Row["FileUniqueKey"],
                            Row["Key"],
                            Row["To"],
                            SqlSafeCodec.Decode(ConvertHelper.ObjToStr(Row["Source"])),
                            SqlSafeCodec.Decode(ConvertHelper.ObjToStr(Row["Result"]))
                        ));
                    }
                }

                return CloudTranslationItems;
            }
            catch { }

            return new List<CloudTranslationItem>();
        }


        public static string FindCacheAndID(int FileUniqueKey, string Key, int To, ref int ID)
        {
            try
            {
                string SqlOrder = "Select Rowid,Result From CloudTranslation Where [FileUniqueKey] = {0} And [Key] = '{1}' And [To] = {2}";

                List<Dictionary<string, object>> GetResult = Engine.LocalDB.ExecuteQuery(string.Format(SqlOrder, FileUniqueKey, Key, To));

                if (GetResult.Count > 0)
                {
                    var Row = GetResult[0];
                    string GetStr = SqlSafeCodec.Decode(ConvertHelper.ObjToStr(Row["Result"]));
                    ID = ConvertHelper.ObjToInt(Row["Rowid"]);
                    return GetStr;
                }

                return string.Empty;
            }
            catch { return string.Empty; }
        }

        public static bool DeleteCacheByID(int Rowid)
        {
            try
            {
                string SqlOrder = "Delete From CloudTranslation Where Rowid = {0}";
                int State = Engine.LocalDB.ExecuteNonQuery(string.Format(SqlOrder, Rowid));
                if (State != 0)
                {
                    return true;
                }
                return false;
            }
            catch { return false; }
        }

        public static bool ClearCloudCache(int FileUniqueKey)
        {
            string SqlOrder = "Delete From CloudTranslation Where [FileUniqueKey] = " + FileUniqueKey + "";
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
