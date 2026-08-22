using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using PhoenixEngine.Common;
using PhoenixEngine.Engine;
using PhoenixEngine.Language;

namespace PhoenixEngine.ADO
{
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
            this.FileUniqueKey = P_Convert.ObjToInt(FileUniqueKey);
            this.Key = P_Convert.ObjToStr(Key);
            this.To = P_Convert.ObjToInt(To);
            this.Result = P_Convert.ObjToStr(Result);
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
            const string CheckTableSql =
                "SELECT name FROM sqlite_master WHERE type = 'table' AND name = @tableName;";
            var Result = Phoenix.LocalDB.ExecuteScalar(
                CheckTableSql,
                SqliteSql.Parameter("@tableName", TableName));

            if (Result != null && Result != DBNull.Value)
            {
                // Table exists, check column structure
                var Columns = Phoenix.LocalDB.ExecuteQuery("PRAGMA table_info(LocalTranslation);");

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
                    Phoenix.LocalDB.ExecuteNonQuery(
                        "DROP TABLE IF EXISTS " + SqliteSql.QuoteIdentifier(TableName) + ";");
                    Phoenix.LocalDB.ExecuteNonQuery(CreateSql);
                }
            }
            else
            {
                // Create if not exists
                Phoenix.LocalDB.ExecuteNonQuery(CreateSql);
            }
        }

        public static List<CloudTranslationItem> MatchLocalItem(int To, string Source, int Limit = 5)
        {
            new TranslationPreprocessor().OptimizeStrings(ref Source);

            try
            {
                List<CloudTranslationItem> CloudTranslationItems = new List<CloudTranslationItem>();

                const string SqlOrder = @"
SELECT * FROM LocalTranslation
WHERE [To] = @to AND [Source] = @source
LIMIT @limit;";
                List<Dictionary<string, object>> NTable = Phoenix.LocalDB.ExecuteQuery(
                    SqlOrder,
                    SqliteSql.Parameter("@to", To),
                    SqliteSql.Parameter("@source", SQLSafeCodec.Encode(Source)),
                    SqliteSql.Parameter("@limit", 5));
                if (NTable.Count > 0)
                {
                    for (int i = 0; i < NTable.Count; i++)
                    {
                        var Row = NTable[i]; 

                        CloudTranslationItems.Add(new CloudTranslationItem(
                            Row["FileUniqueKey"],
                            Row["Key"],
                            Row["To"],
                            SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["Source"])),
                            SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["Result"]))
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
                const string SqlOrder = @"
DELETE FROM LocalTranslation
WHERE [FileUniqueKey] = @fileUniqueKey AND [To] = @to;";
                int State = Phoenix.LocalDB.ExecuteNonQuery(
                    SqlOrder,
                    SqliteSql.Parameter("@fileUniqueKey", FileUniqueKey),
                    SqliteSql.Parameter("@to", (int)TargetLanguage));

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
            new TranslationPreprocessor().OptimizeStrings(ref Source);

            try 
            {
                const string SqlOrder = @"
DELETE FROM LocalTranslation
WHERE [FileUniqueKey] = @fileUniqueKey AND [Source] = @source AND [To] = @to;";
                int State = Phoenix.LocalDB.ExecuteNonQuery(
                    SqlOrder,
                    SqliteSql.Parameter("@fileUniqueKey", FileUniqueKey),
                    SqliteSql.Parameter("@source", SQLSafeCodec.Encode(Source)),
                    SqliteSql.Parameter("@to", (int)TargetLanguage));

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
                const string SqlOrder = @"
DELETE FROM LocalTranslation
WHERE [FileUniqueKey] = @fileUniqueKey AND [Result] = @result AND [To] = @to;";
                int State = Phoenix.LocalDB.ExecuteNonQuery(
                    SqlOrder,
                    SqliteSql.Parameter("@fileUniqueKey", FileUniqueKey),
                    SqliteSql.Parameter("@result", SQLSafeCodec.Encode(ResultText)),
                    SqliteSql.Parameter("@to", (int)TargetLanguage));

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
                const string SqlOrder = @"
DELETE FROM LocalTranslation
WHERE [FileUniqueKey] = @fileUniqueKey AND [Key] = @key AND [To] = @to;";
                int State = Phoenix.LocalDB.ExecuteNonQuery(
                    SqlOrder,
                    SqliteSql.Parameter("@fileUniqueKey", FileUniqueKey),
                    SqliteSql.Parameter("@key", Key),
                    SqliteSql.Parameter("@to", (int)TargetLanguage));

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
                const string SqlOrder = @"
SELECT Result FROM LocalTranslation
WHERE [FileUniqueKey] = @fileUniqueKey AND [Key] = @key AND [To] = @to;";
                string GetText = P_Convert.ObjToStr(Phoenix.LocalDB.ExecuteScalar(
                    SqlOrder,
                    SqliteSql.Parameter("@fileUniqueKey", FileUniqueKey),
                    SqliteSql.Parameter("@key", Key),
                    SqliteSql.Parameter("@to", (int)TargetLanguage)));

                if (GetText.Trim().Length > 0)
                {
                    return SQLSafeCodec.Decode(GetText);
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
                const string SqlOrder = @"
SELECT Result FROM LocalTranslation
WHERE [FileUniqueKey] = @fileUniqueKey AND [Key] = @key AND [To] = @to;";
                string GetResult = P_Convert.ObjToStr(Phoenix.LocalDB.ExecuteScalar(
                    SqlOrder,
                    SqliteSql.Parameter("@fileUniqueKey", FileUniqueKey),
                    SqliteSql.Parameter("@key", Key),
                    SqliteSql.Parameter("@to", To)));

                if (GetResult.Trim().Length > 0)
                {
                    return SQLSafeCodec.Decode(GetResult);
                }

                return string.Empty;
            }
            catch { return string.Empty; }
        }

        public static bool UpdateLocalTransItem(int FileUniqueKey, string Key, int To, string Source, string Result, int Index)
        {
            if (Result.Length > 0)
            {
                new TranslationPreprocessor().OptimizeStrings(ref Source);

                int GetRowID = P_Convert.ObjToInt(Phoenix.LocalDB.ExecuteScalar(
                    @"SELECT Rowid FROM LocalTranslation
WHERE [FileUniqueKey] = @fileUniqueKey AND [Key] = @key AND [To] = @to;",
                    SqliteSql.Parameter("@fileUniqueKey", FileUniqueKey),
                    SqliteSql.Parameter("@key", Key),
                    SqliteSql.Parameter("@to", To)));

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

                    const string SqlOrder = @"
INSERT INTO LocalTranslation ([FileUniqueKey], [Key], [To], [Source], [Result], [Index])
VALUES (@fileUniqueKey, @key, @to, @source, @result, @index);";
                    int State = Phoenix.LocalDB.ExecuteNonQuery(
                        SqlOrder,
                        SqliteSql.Parameter("@fileUniqueKey", FileUniqueKey),
                        SqliteSql.Parameter("@key", Key),
                        SqliteSql.Parameter("@to", To),
                        SqliteSql.Parameter("@source", SQLSafeCodec.Encode(Source)),
                        SqliteSql.Parameter("@result", SQLSafeCodec.Encode(Result)),
                        SqliteSql.Parameter("@index", Index));
                    if (State != 0)
                    {
                        return true;
                    }
                }
                else
                {
                    const string SqlOrder = @"
UPDATE LocalTranslation
SET [Result] = @result, [Index] = @index
WHERE Rowid = @rowid;";
                    int State = Phoenix.LocalDB.ExecuteNonQuery(
                        SqlOrder,
                        SqliteSql.Parameter("@result", SQLSafeCodec.Encode(Result)),
                        SqliteSql.Parameter("@index", Index),
                        SqliteSql.Parameter("@rowid", GetRowID));
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

        public static bool ClearLocalCache(int FileUniqueKey)
        {
            const string SqlOrder =
                "DELETE FROM LocalTranslation WHERE [FileUniqueKey] = @fileUniqueKey;";
            int State = Phoenix.LocalDB.ExecuteNonQuery(
                SqlOrder,
                SqliteSql.Parameter("@fileUniqueKey", FileUniqueKey));
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
