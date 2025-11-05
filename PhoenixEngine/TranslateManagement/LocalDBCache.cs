
using System.Data;
using PhoenixEngine.ConvertManager;
using PhoenixEngine.DataBaseManagement;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.TranslateCore;

namespace PhoenixEngine.TranslateManagement
{
    // Copyright (c) 2025 YD525
    // Licensed under the MIT License.
    // See LICENSE file in the project root for full license information.
    //https://github.com/YD525/PhoenixEngine
    public class LocalTransItem
    {
        public int FileUniqueKey = 0;
        public string Key = "";
        public int To = 0;
        public string Result = "";
        public int Index = 0;

        public LocalTransItem(int FileUniqueKey, string Key, Languages TargetLanguage, string Result)
        {
            this.FileUniqueKey = FileUniqueKey;
            this.Key = Key;
            this.To = (int)TargetLanguage;
            this.Result = Result;
            this.Index = 0;
        }

        public LocalTransItem(object FileUniqueKey, object Key, object To, object Result)
        {
            this.FileUniqueKey = ConvertHelper.ObjToInt(FileUniqueKey);
            this.Key = ConvertHelper.ObjToStr(Key);
            this.To = ConvertHelper.ObjToInt(To);
            this.Result = ConvertHelper.ObjToStr(Result);
            this.Index = 0;
        }
    }
    public class LocalDBCache
    {
        public static void Init()
        {
            string TableName = "LocalTranslation";
            string CreateSql = @"
CREATE TABLE [LocalTranslation](
  [FileUniqueKey] INT, 
  [Key] TEXT, 
  [To] INT, 
  [Source] TEXT, 
  [Result] TEXT, 
  [Index] INT
);";

            // Check if table exists
            string CheckTableSql = $"SELECT name FROM sqlite_master WHERE type='table' AND name='{TableName}';";
            var Result = Engine.LocalDB.ExecuteScalar(CheckTableSql);

            if (Result != null && Result != DBNull.Value)
            {
                // Table exists, check column structure
                var Columns = Engine.LocalDB.ExecuteQuery("PRAGMA table_info(LocalTranslation);");

                // Current columns
                var ExistingCols = new HashSet<string>(
                    Columns.AsEnumerable().Select(R => R["name"].ToString()),
                    StringComparer.OrdinalIgnoreCase
                );

                // Expected columns
                string[] ExpectedCols = { "FileUniqueKey", "Key", "To", "Source", "Result", "Index" };

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

        public static List<CloudTranslationItem> MatchLocalItem(int To, string Source, int Limit = 5)
        {
            try
            {
                List<CloudTranslationItem> CloudTranslationItems = new List<CloudTranslationItem>();

                string SqlOrder = "Select * From LocalTranslation Where [To] = {0} And [Source] = '{1}' Limit 5";
                DataTable NTable = Engine.LocalDB.ExecuteDataTable(string.Format(SqlOrder, To, SqlSafeCodec.Encode(Source)));
                if (NTable.Rows.Count > 0)
                {
                    for (int i = 0; i < NTable.Rows.Count; i++)
                    {
                        CloudTranslationItems.Add(new CloudTranslationItem(
                            NTable.Rows[i]["FileUniqueKey"],
                            NTable.Rows[i]["Key"],
                            NTable.Rows[i]["To"],
                            SqlSafeCodec.Decode(ConvertHelper.ObjToStr(NTable.Rows[i]["Source"])),
                            SqlSafeCodec.Decode(ConvertHelper.ObjToStr(NTable.Rows[i]["Result"]))
                           ));
                    }
                }

                return CloudTranslationItems;
            }
            catch 
            {
                return new List<CloudTranslationItem>();
            }
        }

        public static bool DeleteCacheByFileUniqueKey(int FileUniqueKey, Languages TargetLanguage)
        {
            try
            {
                string SqlOrder = "Delete From LocalTranslation Where [FileUniqueKey] = {0} And [To] = {1}";

                int State = Engine.LocalDB.ExecuteNonQuery(string.Format(SqlOrder, FileUniqueKey, (int)TargetLanguage));

                if (State != 0)
                {
                    return true;
                }

                return false;
            }
            catch { return false; }
        }

        public static bool DeleteCacheBySource(int FileUniqueKey,string Source,Languages TargetLanguage)
        {
            try 
            {
                string SqlOrder = "Delete From LocalTranslation Where [FileUniqueKey] = {0} And [Source] = '{1}' And [To] = {2}";

                int State = Engine.LocalDB.ExecuteNonQuery(string.Format(SqlOrder, FileUniqueKey, SqlSafeCodec.Encode(Source), (int)TargetLanguage));

                if (State != 0)
                {
                    return true;
                }

                return false;

            } catch { return false; }
        }

        public static bool DeleteCacheByResult(int FileUniqueKey, string ResultText, Languages TargetLanguage)
        {
            try
            {
                string SqlOrder = "Delete From LocalTranslation Where [FileUniqueKey] = {0} And [Result] = '{1}' And [To] = {2}";

                int State = Engine.LocalDB.ExecuteNonQuery(string.Format(SqlOrder, FileUniqueKey, SqlSafeCodec.Encode(ResultText), (int)TargetLanguage));

                if (State != 0)
                {
                    return true;
                }

                return false;
            }
            catch { return false; }
        }

        public static bool DeleteCache(int FileUniqueKey, string Key, Languages TargetLanguage)
        {
            try
            {
                string SqlOrder = "Delete From LocalTranslation Where [FileUniqueKey] = {0} And [Key] = '{1}' And [To] = {2}";

                int State = Engine.LocalDB.ExecuteNonQuery(string.Format(SqlOrder, FileUniqueKey, Key, (int)TargetLanguage));

                if (State != 0)
                {
                    return true;
                }

                return false;
            }
            catch { return false; }
        }

        public static string GetCacheText(int FileUniqueKey, string Key, Languages TargetLanguage)
        {
            try
            {
                string SqlOrder = "Select Result From LocalTranslation Where [FileUniqueKey] = {0} And [Key] = '{1}' And [To] = {2}";

                string GetText = ConvertHelper.ObjToStr(Engine.LocalDB.ExecuteScalar(string.Format(SqlOrder, FileUniqueKey, Key, (int)TargetLanguage)));

                if (GetText.Trim().Length > 0)
                {
                    return SqlSafeCodec.Decode(GetText);
                }

                return string.Empty;
            }
            catch { return string.Empty; }
        }

        public static string FindCache(int FileUniqueKey, string Key, Languages TargetLanguage)
        {
            return FindCache(FileUniqueKey, Key, (int)TargetLanguage);
        }


        public static string FindCache(int FileUniqueKey, string Key, int To)
        {
            try
            {
                string SqlOrder = "Select Result From LocalTranslation Where [FileUniqueKey] = {0} And [Key] = '{1}' And [To] = {2}";

                string GetResult = ConvertHelper.ObjToStr(Engine.LocalDB.ExecuteScalar(string.Format(SqlOrder, FileUniqueKey, Key, To)));

                if (GetResult.Trim().Length > 0)
                {
                    return SqlSafeCodec.Decode(GetResult);
                }

                return string.Empty;
            }
            catch { return string.Empty; }
        }

        public static bool UPDateLocalTransItem(int FileUniqueKey, string Key, int To, string Source, string Result, int Index)
        {
            if (Result.Length > 0)
            {
                int GetRowID = ConvertHelper.ObjToInt(Engine.LocalDB.ExecuteScalar(String.Format("Select Rowid From LocalTranslation Where [FileUniqueKey] = '{0}' And [Key] = '{1}' And [To] = {2}", FileUniqueKey, Key, To)));

                if (GetRowID <= 0)
                {
                    var GetStr = CloudDBCache.FindCache(FileUniqueKey, Key, (Languages)To);
                    if (GetStr.Length > 0)
                    {
                        if (GetStr.Equals(Result))
                        {
                            return true;
                        }
                    }

                    string SqlOrder = "Insert Into LocalTranslation([FileUniqueKey],[Key],[To],[Source],[Result],[Index])Values('{0}','{1}',{2},'{3}','{4}',{5})";
                    int State = Engine.LocalDB.ExecuteNonQuery(string.Format(SqlOrder,
                        FileUniqueKey,
                        Key,
                        To,
                        SqlSafeCodec.Encode(Source),
                        SqlSafeCodec.Encode(Result),
                        Index
                        ));
                    if (State != 0)
                    {
                        return true;
                    }
                }
                else
                {
                    string SqlOrder = "UPDate LocalTranslation Set [Result] = '{1}',[Index] = {2} Where Rowid = {0}";
                    int State = Engine.LocalDB.ExecuteNonQuery(string.Format(SqlOrder, GetRowID, SqlSafeCodec.Encode(Result), Index));
                    if (State != 0)
                    {
                        return true;
                    }
                }
            }
            else
            {
                DeleteCache(FileUniqueKey, Key, (Languages)To);
            }

            return false;
        }

    }
}
