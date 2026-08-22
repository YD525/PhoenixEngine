using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using PhoenixEngine.Common;
using PhoenixEngine.Language;

namespace PhoenixEngine.ADO
{
    public class AdvancedDictionaryItem
    {
        public int Rowid = 0;
        public string TargetFileName = "";
        public string Type = "";
        public string Source = "";
        public string Result = "";
        public int From = 0;
        public int To = 0;
        public int ExactMatch = 0;
        public int IgnoreCase = 0;
        public string Regex = "";

        public AdvancedDictionaryItem()
        {

        }
        public AdvancedDictionaryItem(object TargetFileName, object Type, object Source, object Result, object From, object To, object ExactMatch, object IgnoreCase, object Regex)
        {
            this.TargetFileName = P_Convert.ObjToStr(TargetFileName);
            this.Type = P_Convert.ObjToStr(Type);
            this.Source = P_Convert.ObjToStr(Source);
            this.Result = P_Convert.ObjToStr(Result);
            this.From = P_Convert.ObjToInt(From);
            this.To = P_Convert.ObjToInt(To);
            this.ExactMatch = P_Convert.ObjToInt(ExactMatch);
            this.IgnoreCase = P_Convert.ObjToInt(IgnoreCase);
            this.Regex = P_Convert.ObjToStr(Regex);
        }
        public AdvancedDictionaryItem(object Rowid,object TargetFileName, object Type, object Source, object Result, object From, object To, object ExactMatch, object IgnoreCase, object Regex)
        {
            this.Rowid = P_Convert.ObjToInt(Rowid);
            this.TargetFileName = P_Convert.ObjToStr(TargetFileName);
            this.Type = P_Convert.ObjToStr(Type);
            this.Source = P_Convert.ObjToStr(Source);
            this.Result = P_Convert.ObjToStr(Result);
            this.From = P_Convert.ObjToInt(From);
            this.To = P_Convert.ObjToInt(To);
            this.ExactMatch = P_Convert.ObjToInt(ExactMatch);
            this.IgnoreCase = P_Convert.ObjToInt(IgnoreCase);
            this.Regex = P_Convert.ObjToStr(Regex);
        }
    }
    public class AdvancedDictionary
    {
        public static void Init()
        {
            const string CheckTableSql =
                "SELECT name FROM sqlite_master WHERE type = 'table' AND name = @tableName;";
            var Result = Phoenix.LocalDB.ExecuteScalar(
                CheckTableSql,
                SqliteSql.Parameter("@tableName", "AdvancedDictionary"));

            if (Result == null || Result == DBNull.Value)
            {
                //If the table doesn't exist, create a new one
                CreateNewTable();
            }
            else
            {
                //Table exists, check whether it's the old structure (has TargetModName instead of TargetFileName)
                string CheckOldColumnSql = "PRAGMA table_info(AdvancedDictionary);";
                var dt = Phoenix.LocalDB.ExecuteQuery(CheckOldColumnSql);

                bool HasTargetFileName = dt.Any(r => r["name"].ToString() == "TargetFileName");
                bool HasTargetModName = dt.Any(r => r["name"].ToString() == "TargetModName");

                if (!HasTargetFileName && HasTargetModName)
                {
                    //Detected old table structure, migrate data to the new structure
                    MigrateOldTable();
                }
                else if (!HasTargetFileName)
                {
                    //Table structure is broken or unknown, recreate a new one
                    RecreateNewTable();
                }
            }
        }

        private static void CreateNewTable()
        {
            string SqlOrder = @"
CREATE TABLE [AdvancedDictionary](
  [TargetFileName] TEXT, 
  [Type] TEXT, 
  [Source] TEXT, 
  [Result] TEXT, 
  [From] INT, 
  [To] INT, 
  [ExactMatch] INT, 
  [IgnoreCase] INT, 
  [Regex] TEXT
);";
            Phoenix.LocalDB.ExecuteNonQuery(SqlOrder);
        }

        private static void MigrateOldTable()
        {
            //Rename the old table
            Phoenix.LocalDB.ExecuteNonQuery("ALTER TABLE AdvancedDictionary RENAME TO AdvancedDictionary_Old;");

            //Create a new table with the updated structure
            CreateNewTable();

            //Migrate data from the old table to the new table
            string SqlOrder = @"
INSERT INTO AdvancedDictionary
(TargetFileName, Type, Source, Result, [From], [To], ExactMatch, IgnoreCase, Regex)
SELECT TargetModName, Type, Source, Result, [From], [To], ExactMatch, IgnoreCase, Regex
FROM AdvancedDictionary_Old;";

            Phoenix.LocalDB.ExecuteNonQuery(SqlOrder);

            //Drop the old table after migration
            Phoenix.LocalDB.ExecuteNonQuery("DROP TABLE AdvancedDictionary_Old;");
        }

        private static void RecreateNewTable()
        {
            //Defensive fallback: drop the broken table and recreate it
            Phoenix.LocalDB.ExecuteNonQuery("DROP TABLE IF EXISTS AdvancedDictionary;");
            CreateNewTable();
        }

        public static string GetSourceByRowid(int Rowid)
        {
            const string SqlOrder = "SELECT [Source] FROM AdvancedDictionary WHERE Rowid = @rowid;";
            return SQLSafeCodec.Decode(P_Convert.ObjToStr(Phoenix.LocalDB.ExecuteScalar(
                SqlOrder,
                SqliteSql.Parameter("@rowid", Rowid))));
        }
        public static bool IsRegexMatch(string Input, string SetRegex)
        {
            try
            {
                return Regex.IsMatch(Input, SetRegex);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static AdvancedDictionaryItem ExactMatch(Languages From,Languages To,string Type,string Source)
        {
            const string SqlOrder = @"
SELECT Rowid, * FROM AdvancedDictionary
WHERE [From] = @from
AND [To] = @to
AND ([Type] IS NULL OR [Type] = '' OR [Type] = @type)
AND [Source] = @source
LIMIT 1;";

            List<Dictionary<string, object>> NTable = Phoenix.LocalDB.ExecuteQuery(
                SqlOrder,
                SqliteSql.Parameter("@from", (int)From),
                SqliteSql.Parameter("@to", (int)To),
                SqliteSql.Parameter("@type", SQLSafeCodec.Encode(Type)),
                SqliteSql.Parameter("@source", SQLSafeCodec.Encode(Source)));
            if (NTable.Count > 0)
            {
                var Row = NTable[0]; // row is Dictionary<string, object>

                return new AdvancedDictionaryItem(
                    Row["Rowid"],
                    SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["TargetFileName"])),
                    SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["Type"])),
                    SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["Source"])),
                    SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["Result"])),
                    Row["From"],
                    Row["To"],
                    Row["ExactMatch"],
                    Row["IgnoreCase"],
                    SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["Regex"]))
                );
            }

            return null;
        }

        public static void KeepLongestMatches(string SourceText, ref List<AdvancedDictionaryItem> Items)
        {
            if (Items == null || Items.Count <= 1) return;

            var SortedItems = Items.OrderByDescending(x => x.Source.Length).ToList();
            var FinalList = new List<AdvancedDictionaryItem>();

            foreach (var Current in SortedItems)
            {
                string CurrentSource = Current.Source;
                
                StringComparison ComparisonMode = Current.IgnoreCase == 1 ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

                int MatchStart = SourceText.IndexOf(CurrentSource, ComparisonMode);
                if (MatchStart < 0) continue;

                int MatchEnd = MatchStart + CurrentSource.Length;

                bool Overlaps = FinalList.Any(Item =>
                {
                    StringComparison ItemComparison = Item.IgnoreCase == 1 ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                    int ItemStart = SourceText.IndexOf(Item.Source, ItemComparison);
                    int ItemEnd = ItemStart + Item.Source.Length;
                    return MatchStart < ItemEnd && ItemStart < MatchEnd;
                });

                if (!Overlaps)
                {
                    FinalList.Add(Current);
                }
            }

            Items = FinalList;
        }

        public static List<AdvancedDictionaryItem> Query(string FileName, string Type, Languages From, Languages To, string SourceText,bool UseWordBoundary)
        {
            List<AdvancedDictionaryItem> AdvancedDictionaryItems = new List<AdvancedDictionaryItem>();

            string SqlOrder = "";

            if (!UseWordBoundary)
            {
                SqlOrder = @"
SELECT Rowid,* FROM AdvancedDictionary
WHERE 
  (
    TargetFileName IS NULL
    OR TargetFileName = ''
    OR TargetFileName = @fileName
  )
  AND (
    [Type] IS NULL
    OR [Type] = ''
    OR [Type] = @type
  )
  AND [From] = @from
  AND [To] = @to
  AND (
    (ExactMatch = 1 AND (
      (IgnoreCase = 1 AND LOWER(Source) = LOWER(@sourceText))
      OR (IgnoreCase = 0 AND Source = @sourceText)
    ))
    OR
    (ExactMatch = 0 AND (
      (IgnoreCase = 1 AND LOWER(@sourceText) LIKE '%' || LOWER(Source) || '%')
      OR (IgnoreCase = 0 AND @sourceText LIKE '%' || Source || '%')
    ))
  )
";
            }
            else
            {
                SqlOrder = @"
SELECT Rowid,* FROM AdvancedDictionary
WHERE 
  (
    TargetFileName IS NULL
    OR TargetFileName = ''
    OR TargetFileName = @fileName
  )
  AND (
    [Type] IS NULL
    OR [Type] = ''
    OR [Type] = @type
  )
  AND [From] = @from
  AND [To] = @to
  AND (
    -- Exact match
    (
      ExactMatch = 1 AND (
        (IgnoreCase = 1 AND LOWER(Source) = LOWER(@sourceText))
        OR (IgnoreCase = 0 AND Source = @sourceText)
      )
    )
    OR
    -- Non-exact match with word boundary
    (
      ExactMatch = 0 AND (
        (
          IgnoreCase = 1
          AND INSTR(LOWER(@sourceText), LOWER(Source)) > 0
          AND (
            -- Left boundary: start of string or non-word character
            INSTR(LOWER(@sourceText), LOWER(Source)) = 1
            OR SUBSTR(
                 LOWER(@sourceText),
                 INSTR(LOWER(@sourceText), LOWER(Source)) - 1,
                 1
               ) NOT GLOB '[a-z0-9_]'
          )
          AND (
            -- Right boundary: end of string or non-word character
            INSTR(LOWER(@sourceText), LOWER(Source)) + LENGTH(Source) - 1 = LENGTH(@sourceText)
            OR SUBSTR(
                 LOWER(@sourceText),
                 INSTR(LOWER(@sourceText), LOWER(Source)) + LENGTH(Source),
                 1
               ) NOT GLOB '[a-z0-9_]'
          )
        )
        OR
        (
          IgnoreCase = 0
          AND INSTR(@sourceText, Source) > 0
          AND (
            -- Left boundary: start of string or non-word character
            INSTR(@sourceText, Source) = 1
            OR SUBSTR(
                 @sourceText,
                 INSTR(@sourceText, Source) - 1,
                 1
               ) NOT GLOB '[A-Za-z0-9_]'
          )
          AND (
            -- Right boundary: end of string or non-word character
            INSTR(@sourceText, Source) + LENGTH(Source) - 1 = LENGTH(@sourceText)
            OR SUBSTR(
                 @sourceText,
                 INSTR(@sourceText, Source) + LENGTH(Source),
                 1
               ) NOT GLOB '[A-Za-z0-9_]'
          )
        )
      )
    )
  )
";
            }

            List<Dictionary<string, object>> NTable = Phoenix.LocalDB.ExecuteQuery(
                SqlOrder,
                SqliteSql.Parameter("@fileName", SQLSafeCodec.Encode(FileName)),
                SqliteSql.Parameter("@type", SQLSafeCodec.Encode(Type)),
                SqliteSql.Parameter("@from", (int)From),
                SqliteSql.Parameter("@to", (int)To),
                SqliteSql.Parameter("@sourceText", SQLSafeCodec.Encode(SourceText)));

            for (int i = 0; i < NTable.Count; i++)
            {
                var Row = NTable[i];
                var Get = new AdvancedDictionaryItem(
                Row["Rowid"],
                SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["TargetFileName"])),
                SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["Type"])),
                SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["Source"])),
                SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["Result"])),
                Row["From"],
                Row["To"],
                Row["ExactMatch"],
                Row["IgnoreCase"],
                SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["Regex"]))
            );
                if (Get.Regex.Trim().Length > 0)
                {
                    if (IsRegexMatch(SourceText,System.Web.HttpUtility.HtmlDecode(Get.Regex)))
                    {
                        AdvancedDictionaryItems.Add(Get);
                    }
                }
                else
                {
                    AdvancedDictionaryItems.Add(Get);
                }
            }

            KeepLongestMatches(SourceText, ref AdvancedDictionaryItems);

            return AdvancedDictionaryItems;
        }

        public static bool CheckSame(AdvancedDictionaryItem item)
        {
            const string CheckSql = @"
SELECT COUNT(*) FROM AdvancedDictionary 
WHERE 
[TargetFileName] = @targetFileName AND
[Type] = @type AND
[Source] = @source AND
[Result] = @result AND
[From] = @from AND
[To] = @to;";

            int Count = Convert.ToInt32(Phoenix.LocalDB.ExecuteScalar(
                CheckSql,
                SqliteSql.Parameter("@targetFileName", SQLSafeCodec.Encode(item.TargetFileName)),
                SqliteSql.Parameter("@type", SQLSafeCodec.Encode(item.Type)),
                SqliteSql.Parameter("@source", SQLSafeCodec.Encode(item.Source)),
                SqliteSql.Parameter("@result", SQLSafeCodec.Encode(item.Result)),
                SqliteSql.Parameter("@from", item.From),
                SqliteSql.Parameter("@to", item.To)));
            return Count > 0;
        }


        public static bool AddItem(AdvancedDictionaryItem Item)
        {
            if (!CheckSame(Item))
            {
                const string sql = @"INSERT INTO AdvancedDictionary
([TargetFileName], [Type], [Source], [Result], [From], [To], [ExactMatch], [IgnoreCase], [Regex])
VALUES (
@targetFileName,
@type,
@source,
@result,
@from,
@to,
@exactMatch,
@ignoreCase,
@regex
);";
                int State = Phoenix.LocalDB.ExecuteNonQuery(
                    sql,
                    SqliteSql.Parameter("@targetFileName", SQLSafeCodec.Encode(Item.TargetFileName)),
                    SqliteSql.Parameter("@type", SQLSafeCodec.Encode(Item.Type)),
                    SqliteSql.Parameter("@source", SQLSafeCodec.Encode(Item.Source)),
                    SqliteSql.Parameter("@result", SQLSafeCodec.Encode(Item.Result)),
                    SqliteSql.Parameter("@from", Item.From),
                    SqliteSql.Parameter("@to", Item.To),
                    SqliteSql.Parameter("@exactMatch", Item.ExactMatch),
                    SqliteSql.Parameter("@ignoreCase", Item.IgnoreCase),
                    SqliteSql.Parameter("@regex", SQLSafeCodec.Encode(Item.Regex)));
                if (State != 0)
                {
                    return true;
                }
                return false;
            }
            else
            {
                return false;
            }
        }

        public static void DeleteItem(AdvancedDictionaryItem item)
        {
            const string sql = @"DELETE FROM AdvancedDictionary WHERE
TargetFileName = @targetFileName AND
Type = @type AND
Source = @source AND
Result = @result AND
[From] = @from AND
[To] = @to AND
ExactMatch = @exactMatch AND
IgnoreCase = @ignoreCase AND
Regex = @regex;";
            Phoenix.LocalDB.ExecuteNonQuery(
                sql,
                SqliteSql.Parameter("@targetFileName", SQLSafeCodec.Encode(item.TargetFileName)),
                SqliteSql.Parameter("@type", SQLSafeCodec.Encode(item.Type)),
                SqliteSql.Parameter("@source", SQLSafeCodec.Encode(item.Source)),
                SqliteSql.Parameter("@result", SQLSafeCodec.Encode(item.Result)),
                SqliteSql.Parameter("@from", item.From),
                SqliteSql.Parameter("@to", item.To),
                SqliteSql.Parameter("@exactMatch", item.ExactMatch),
                SqliteSql.Parameter("@ignoreCase", item.IgnoreCase),
                SqliteSql.Parameter("@regex", SQLSafeCodec.Encode(item.Regex)));
        }

        public static P_SQL_Page<List<AdvancedDictionaryItem>> QueryByPage(int From, int To, int PageNo)
        {
            const string Where = SqliteSql.LanguageFilter;

            int MaxPage = P_SQL_Pagination.GetPageCount(
                "AdvancedDictionary",
                Where,
                SqliteSql.Parameter("@from", From),
                SqliteSql.Parameter("@to", To));

            List<Dictionary<string, object>> NTable = P_SQL_Pagination.GetTablePageData(
                "AdvancedDictionary",
                PageNo,
                Phoenix.Config.DefPageSize,
                Where,
                SqliteSql.Parameter("@from", From),
                SqliteSql.Parameter("@to", To));

            List<AdvancedDictionaryItem> Items = new List<AdvancedDictionaryItem>();
            for (int i = 0; i < NTable.Count; i++)
            {
                var Row = NTable[i]; // row 是 Dictionary<string, object>

                Items.Add(new AdvancedDictionaryItem(
                    Row["Rowid"],
                    SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["TargetFileName"])),
                    SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["Type"])),
                    SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["Source"])),
                    SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["Result"])),
                    Row["From"],
                    Row["To"],
                    Row["ExactMatch"],
                    Row["IgnoreCase"],
                    SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["Regex"]))
                ));
            }

            return new P_SQL_Page<List<AdvancedDictionaryItem>>(Items, PageNo, MaxPage);
        }

        public static P_SQL_Page<List<AdvancedDictionaryItem>> QueryByPage(string SourceText,int From,int To, int PageNo)
        {
            const string Where = SqliteSql.SourceLanguageFilter;

            int MaxPage = P_SQL_Pagination.GetPageCount(
                "AdvancedDictionary",
                Where,
                SqliteSql.Parameter("@source", SQLSafeCodec.Encode(SourceText)),
                SqliteSql.Parameter("@from", From),
                SqliteSql.Parameter("@to", To));

            List<Dictionary<string, object>> NTable = P_SQL_Pagination.GetTablePageData(
                "AdvancedDictionary",
                PageNo,
                Phoenix.Config.DefPageSize,
                Where,
                SqliteSql.Parameter("@source", SQLSafeCodec.Encode(SourceText)),
                SqliteSql.Parameter("@from", From),
                SqliteSql.Parameter("@to", To));

            List<AdvancedDictionaryItem> Items = new List<AdvancedDictionaryItem>();
            for (int i = 0; i < NTable.Count; i++)
            {
                var Row = NTable[i];

                Items.Add(new AdvancedDictionaryItem(
                    SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["TargetFileName"])),
                    SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["Type"])),
                    SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["Source"])),
                    SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["Result"])),
                    Row["From"],
                    Row["To"],
                    Row["ExactMatch"],
                    Row["IgnoreCase"],
                    SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["Regex"]))
                ));
            }

            return new P_SQL_Page<List<AdvancedDictionaryItem>>(Items, PageNo, MaxPage);
        }

        public static bool DeleteByRowid(int Rowid)
        {
            const string SqlOrder = "DELETE FROM AdvancedDictionary WHERE Rowid = @rowid;";
            int State = Phoenix.LocalDB.ExecuteNonQuery(
                SqlOrder,
                SqliteSql.Parameter("@rowid", Rowid));
            if (State != 0)
            {
                return true;
            }
            return false;
        }

    }
}
