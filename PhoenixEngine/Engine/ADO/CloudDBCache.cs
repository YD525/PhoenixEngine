using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using PhoenixEngine.Common;
using PhoenixEngine.Engine;
using PhoenixEngine.Language;

namespace PhoenixEngine.ADO
{
    public class CloudTranslationItem
    {
        public int FileUniqueKey = 0;
        public string Key = "";
        public int To = 0;
        public string Source = "";
        public string Result = "";

        public CloudTranslationItem(object FileUniqueKey, object Key, object To, object Source, object Result)
        {
            this.FileUniqueKey = P_Convert.ObjToInt(FileUniqueKey);
            this.Key = P_Convert.ObjToStr(Key);
            this.To = P_Convert.ObjToInt(To);
            this.Source = P_Convert.ObjToStr(Source);
            this.Result = P_Convert.ObjToStr(Result);
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
            const string CheckTableSql =
                "SELECT name FROM sqlite_master WHERE type = 'table' AND name = @tableName;";
            var Result = Phoenix.LocalDB.ExecuteScalar(
                CheckTableSql,
                SqliteSql.Parameter("@tableName", TableName));

            if (Result != null && Result != DBNull.Value)
            {
                // Table exists, check structure
                string QuotedTableName = SqliteSql.QuoteIdentifier(TableName);
                List<Dictionary<string, object>> Columns =
                    Phoenix.LocalDB.ExecuteQuery("PRAGMA table_info(" + QuotedTableName + ");");
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
                    Phoenix.LocalDB.ExecuteNonQuery("DROP TABLE IF EXISTS " + QuotedTableName + ";");
                    Phoenix.LocalDB.ExecuteNonQuery(CreateSql);
                }
            }
            else
            {
                // Create if not exists
                Phoenix.LocalDB.ExecuteNonQuery(CreateSql);
            }
        }

        public static bool DeleteCache(int FileUniqueKey, string Key, Languages TargetLanguage)
        {
            try
            {
                const string SqlOrder = @"
DELETE FROM CloudTranslation
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
        public static string FindCache(int FileUniqueKey, string Key, Languages TargetLanguage)
        {
            try
            {
                const string SqlOrder = @"
SELECT Result FROM CloudTranslation
WHERE [FileUniqueKey] = @fileUniqueKey AND [Key] = @key AND [To] = @to;";
                string GetResult = P_Convert.ObjToStr(Phoenix.LocalDB.ExecuteScalar(
                    SqlOrder,
                    SqliteSql.Parameter("@fileUniqueKey", FileUniqueKey),
                    SqliteSql.Parameter("@key", Key),
                    SqliteSql.Parameter("@to", (int)TargetLanguage)));

                if (GetResult.Trim().Length > 0)
                {
                    return SQLSafeCodec.Decode(GetResult);
                }

                return string.Empty;
            }
            catch { return string.Empty; }
        }

        public static bool AddCache(int FileUniqueKey, string Key, int To, string Source, string Result)
        {
            try
            {
                new TranslationPreprocessor().OptimizeStrings(ref Source);

                int GetRowID = P_Convert.ObjToInt(Phoenix.LocalDB.ExecuteScalar(
                    @"SELECT Rowid FROM CloudTranslation
WHERE [FileUniqueKey] = @fileUniqueKey AND [Key] = @key AND [To] = @to;",
                    SqliteSql.Parameter("@fileUniqueKey", FileUniqueKey),
                    SqliteSql.Parameter("@key", Key),
                    SqliteSql.Parameter("@to", To)));

                if (GetRowID <= 0)
                {
                    const string SqlOrder = @"
INSERT INTO CloudTranslation ([FileUniqueKey], [Key], [To], [Source], [Result])
VALUES (@fileUniqueKey, @key, @to, @source, @result);";
                    int State = Phoenix.LocalDB.ExecuteNonQuery(
                        SqlOrder,
                        SqliteSql.Parameter("@fileUniqueKey", FileUniqueKey),
                        SqliteSql.Parameter("@key", Key),
                        SqliteSql.Parameter("@to", To),
                        SqliteSql.Parameter("@source", SQLSafeCodec.Encode(Source)),
                        SqliteSql.Parameter("@result", SQLSafeCodec.Encode(Result)));

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
                new TranslationPreprocessor().OptimizeStrings(ref Source);

                List<CloudTranslationItem> CloudTranslationItems = new List<CloudTranslationItem>();

                const string SqlOrder = @"
SELECT * FROM CloudTranslation
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
            catch { }

            return new List<CloudTranslationItem>();
        }

        public static CloudTranslationItem Match(int To, string Source)
        {
            try
            {
                new TranslationPreprocessor().OptimizeStrings(ref Source);

                const string SqlOrder = @"
SELECT * FROM CloudTranslation
WHERE [To] = @to AND [Source] = @source
LIMIT 1;";
                List<Dictionary<string, object>> NTable = Phoenix.LocalDB.ExecuteQuery(
                    SqlOrder,
                    SqliteSql.Parameter("@to", To),
                    SqliteSql.Parameter("@source", SQLSafeCodec.Encode(Source)));
                if (NTable.Count > 0)
                {
                    var Row = NTable[0];
                    return new CloudTranslationItem(
                        Row["FileUniqueKey"],
                        Row["Key"],
                        Row["To"],
                        SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["Source"])),
                        SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["Result"]))
                    );
                }

                return null;
            }
            catch { }

            return null;
        }

        public static List<CloudTranslationItem> MatchOtherCloudItem(int Rowid,int To, string Source, int Limit = 5)
        {
            try
            {
                new TranslationPreprocessor().OptimizeStrings(ref Source);

                List<CloudTranslationItem> CloudTranslationItems = new List<CloudTranslationItem>();

                const string SqlOrder = @"
SELECT * FROM CloudTranslation
WHERE [To] = @to AND [Source] = @source AND Rowid != @rowid
LIMIT @limit;";
                List<Dictionary<string, object>> NTable = Phoenix.LocalDB.ExecuteQuery(
                    SqlOrder,
                    SqliteSql.Parameter("@to", To),
                    SqliteSql.Parameter("@source", SQLSafeCodec.Encode(Source)),
                    SqliteSql.Parameter("@rowid", Rowid),
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
            catch { }

            return new List<CloudTranslationItem>();
        }


        public static string FindCacheAndID(int FileUniqueKey, string Key, int To, ref int ID)
        {
            try
            {
                const string SqlOrder = @"
SELECT Rowid, Result FROM CloudTranslation
WHERE [FileUniqueKey] = @fileUniqueKey AND [Key] = @key AND [To] = @to;";
                List<Dictionary<string, object>> GetResult = Phoenix.LocalDB.ExecuteQuery(
                    SqlOrder,
                    SqliteSql.Parameter("@fileUniqueKey", FileUniqueKey),
                    SqliteSql.Parameter("@key", Key),
                    SqliteSql.Parameter("@to", To));

                if (GetResult.Count > 0)
                {
                    var Row = GetResult[0];
                    string GetStr = SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["Result"]));
                    ID = P_Convert.ObjToInt(Row["Rowid"]);
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
                const string SqlOrder = "DELETE FROM CloudTranslation WHERE Rowid = @rowid;";
                int State = Phoenix.LocalDB.ExecuteNonQuery(
                    SqlOrder,
                    SqliteSql.Parameter("@rowid", Rowid));
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
            const string SqlOrder =
                "DELETE FROM CloudTranslation WHERE [FileUniqueKey] = @fileUniqueKey;";
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
