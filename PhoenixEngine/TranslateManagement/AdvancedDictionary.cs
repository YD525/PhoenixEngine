using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
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
            this.TargetFileName = ConvertHelper.ObjToStr(TargetFileName);
            this.Type = ConvertHelper.ObjToStr(Type);
            this.Source = ConvertHelper.ObjToStr(Source);
            this.Result = ConvertHelper.ObjToStr(Result);
            this.From = ConvertHelper.ObjToInt(From);
            this.To = ConvertHelper.ObjToInt(To);
            this.ExactMatch = ConvertHelper.ObjToInt(ExactMatch);
            this.IgnoreCase = ConvertHelper.ObjToInt(IgnoreCase);
            this.Regex = ConvertHelper.ObjToStr(Regex);
        }
        public AdvancedDictionaryItem(object Rowid,object TargetFileName, object Type, object Source, object Result, object From, object To, object ExactMatch, object IgnoreCase, object Regex)
        {
            this.Rowid = ConvertHelper.ObjToInt(Rowid);
            this.TargetFileName = ConvertHelper.ObjToStr(TargetFileName);
            this.Type = ConvertHelper.ObjToStr(Type);
            this.Source = ConvertHelper.ObjToStr(Source);
            this.Result = ConvertHelper.ObjToStr(Result);
            this.From = ConvertHelper.ObjToInt(From);
            this.To = ConvertHelper.ObjToInt(To);
            this.ExactMatch = ConvertHelper.ObjToInt(ExactMatch);
            this.IgnoreCase = ConvertHelper.ObjToInt(IgnoreCase);
            this.Regex = ConvertHelper.ObjToStr(Regex);
        }
    }
    public class AdvancedDictionary
    {
        public static void Init()
        {
            string CheckTableSql = "SELECT name FROM sqlite_master WHERE type='table' AND name='AdvancedDictionary';";
            var Result = Engine.LocalDB.ExecuteScalar(CheckTableSql);

            if (Result == null || Result == DBNull.Value)
            {
                //If the table doesn't exist, create a new one
                CreateNewTable();
            }
            else
            {
                //Table exists, check whether it's the old structure (has TargetModName instead of TargetFileName)
                string CheckOldColumnSql = "PRAGMA table_info(AdvancedDictionary);";
                var dt = Engine.LocalDB.ExecuteQuery(CheckOldColumnSql);

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
            Engine.LocalDB.ExecuteNonQuery(SqlOrder);
        }

        private static void MigrateOldTable()
        {
            //Rename the old table
            Engine.LocalDB.ExecuteNonQuery("ALTER TABLE AdvancedDictionary RENAME TO AdvancedDictionary_Old;");

            //Create a new table with the updated structure
            CreateNewTable();

            //Migrate data from the old table to the new table
            string SqlOrder = @"
INSERT INTO AdvancedDictionary
(TargetFileName, Type, Source, Result, [From], [To], ExactMatch, IgnoreCase, Regex)
SELECT TargetModName, Type, Source, Result, [From], [To], ExactMatch, IgnoreCase, Regex
FROM AdvancedDictionary_Old;";

            Engine.LocalDB.ExecuteNonQuery(SqlOrder);

            //Drop the old table after migration
            Engine.LocalDB.ExecuteNonQuery("DROP TABLE AdvancedDictionary_Old;");
        }

        private static void RecreateNewTable()
        {
            //Defensive fallback: drop the broken table and recreate it
            Engine.LocalDB.ExecuteNonQuery("DROP TABLE IF EXISTS AdvancedDictionary;");
            CreateNewTable();
        }

        public static string GetSourceByRowid(int Rowid)
        {
            string SqlOrder = "Select [Source] From AdvancedDictionary Where Rowid = {0}";
            return SqlSafeCodec.Decode(ConvertHelper.ObjToStr(Engine.LocalDB.ExecuteScalar(string.Format(SqlOrder,Rowid))));
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
            string SqlOrder = "Select Rowid,* From AdvancedDictionary Where [ExactMatch] = 1 And [From] = {0} And [To] = {1} And ([Type] Is NULL OR [Type] = '' OR [Type] = '{2}') And [Source] = '{3}' And [IgnoreCase] = 1 Limit 1";

            List<Dictionary<string, object>> NTable = Engine.LocalDB.ExecuteQuery(string.Format(SqlOrder,(int)From,(int)To,SqlSafeCodec.Encode(Type),SqlSafeCodec.Encode(Source)));
            if (NTable.Count > 0)
            {
                var Row = NTable[0]; // row is Dictionary<string, object>

                return new AdvancedDictionaryItem(
                    Row["Rowid"],
                    SqlSafeCodec.Decode(ConvertHelper.ObjToStr(Row["TargetFileName"])),
                    SqlSafeCodec.Decode(ConvertHelper.ObjToStr(Row["Type"])),
                    SqlSafeCodec.Decode(ConvertHelper.ObjToStr(Row["Source"])),
                    SqlSafeCodec.Decode(ConvertHelper.ObjToStr(Row["Result"])),
                    Row["From"],
                    Row["To"],
                    Row["ExactMatch"],
                    Row["IgnoreCase"],
                    SqlSafeCodec.Decode(ConvertHelper.ObjToStr(Row["Regex"]))
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
    OR TargetFileName = '{0}'
  )
  AND (
    [Type] IS NULL
    OR [Type] = ''
    OR [Type] = '{1}'
  )
  AND [From] = {2}
  AND [To] = {3}
  AND (
    (ExactMatch = 1 AND (
      (IgnoreCase = 1 AND LOWER(Source) = LOWER('{4}'))
      OR (IgnoreCase = 0 AND Source = '{4}')
    ))
    OR
    (ExactMatch = 0 AND (
      (IgnoreCase = 1 AND LOWER('{4}') LIKE '%' || LOWER(Source) || '%')
      OR (IgnoreCase = 0 AND '{4}' LIKE '%' || Source || '%')
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
    OR TargetFileName = '{0}'
  )
  AND (
    [Type] IS NULL
    OR [Type] = ''
    OR [Type] = '{1}'
  )
  AND [From] = {2}
  AND [To] = {3}
  AND (
    -- Exact match
    (
      ExactMatch = 1 AND (
        (IgnoreCase = 1 AND LOWER(Source) = LOWER('{4}'))
        OR (IgnoreCase = 0 AND Source = '{4}')
      )
    )
    OR
    -- Non-exact match with word boundary
    (
      ExactMatch = 0 AND (
        (
          IgnoreCase = 1
          AND INSTR(LOWER('{4}'), LOWER(Source)) > 0
          AND (
            -- Left boundary: start of string or non-word character
            INSTR(LOWER('{4}'), LOWER(Source)) = 1
            OR SUBSTR(
                 LOWER('{4}'),
                 INSTR(LOWER('{4}'), LOWER(Source)) - 1,
                 1
               ) NOT GLOB '[a-z0-9_]'
          )
          AND (
            -- Right boundary: end of string or non-word character
            INSTR(LOWER('{4}'), LOWER(Source)) + LENGTH(Source) - 1 = LENGTH('{4}')
            OR SUBSTR(
                 LOWER('{4}'),
                 INSTR(LOWER('{4}'), LOWER(Source)) + LENGTH(Source),
                 1
               ) NOT GLOB '[a-z0-9_]'
          )
        )
        OR
        (
          IgnoreCase = 0
          AND INSTR('{4}', Source) > 0
          AND (
            -- Left boundary: start of string or non-word character
            INSTR('{4}', Source) = 1
            OR SUBSTR(
                 '{4}',
                 INSTR('{4}', Source) - 1,
                 1
               ) NOT GLOB '[A-Za-z0-9_]'
          )
          AND (
            -- Right boundary: end of string or non-word character
            INSTR('{4}', Source) + LENGTH(Source) - 1 = LENGTH('{4}')
            OR SUBSTR(
                 '{4}',
                 INSTR('{4}', Source) + LENGTH(Source),
                 1
               ) NOT GLOB '[A-Za-z0-9_]'
          )
        )
      )
    )
  )
";
            }

                List<Dictionary<string, object>> NTable = Engine.LocalDB.ExecuteQuery(string.Format(
                SqlOrder,
                SqlSafeCodec.Encode(FileName),
                SqlSafeCodec.Encode(Type),
                (int)From,
                (int)To,
                SqlSafeCodec.Encode(SourceText)
            ));

            for (int i = 0; i < NTable.Count; i++)
            {
                var Row = NTable[i];
                var Get = new AdvancedDictionaryItem(
                Row["Rowid"],
                SqlSafeCodec.Decode(ConvertHelper.ObjToStr(Row["TargetFileName"])),
                SqlSafeCodec.Decode(ConvertHelper.ObjToStr(Row["Type"])),
                SqlSafeCodec.Decode(ConvertHelper.ObjToStr(Row["Source"])),
                SqlSafeCodec.Decode(ConvertHelper.ObjToStr(Row["Result"])),
                Row["From"],
                Row["To"],
                Row["ExactMatch"],
                Row["IgnoreCase"],
                SqlSafeCodec.Decode(ConvertHelper.ObjToStr(Row["Regex"]))
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
            string CheckSql = $@"
SELECT COUNT(*) FROM AdvancedDictionary 
WHERE 
[TargetFileName] = '{SqlSafeCodec.Encode(item.TargetFileName)}' AND
[Type] = '{SqlSafeCodec.Encode(item.Type)}' AND
[Source] = '{SqlSafeCodec.Encode(item.Source)}' AND
[Result] = '{SqlSafeCodec.Encode(item.Result)}' AND
[From] = {item.From} AND
[To] = {item.To}";

            int Count = Convert.ToInt32(Engine.LocalDB.ExecuteScalar(CheckSql));
            return Count > 0;
        }


        public static bool AddItem(AdvancedDictionaryItem Item)
        {
            if (!CheckSame(Item))
            {
                string sql = $@"INSERT INTO AdvancedDictionary 
([TargetFileName], [Type], [Source], [Result], [From], [To], [ExactMatch], [IgnoreCase], [Regex])
VALUES (
'{SqlSafeCodec.Encode(Item.TargetFileName)}',
'{SqlSafeCodec.Encode(Item.Type)}',
'{SqlSafeCodec.Encode(Item.Source)}',
'{SqlSafeCodec.Encode(Item.Result)}',
{Item.From},
{Item.To},
{Item.ExactMatch},
{Item.IgnoreCase},
'{SqlSafeCodec.Encode(Item.Regex)}'
)";
                int State = Engine.LocalDB.ExecuteNonQuery(sql);
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
            string sql = $@"DELETE FROM AdvancedDictionary WHERE 
TargetFileName = '{SqlSafeCodec.Encode(item.TargetFileName)}' AND
Type = '{SqlSafeCodec.Encode(item.Type)}' AND
Source = '{SqlSafeCodec.Encode(item.Source)}' AND
Result = '{SqlSafeCodec.Encode(item.Result)}' AND
[From] = {item.From} AND
[To] = {item.To} AND
ExactMatch = {item.ExactMatch} AND
IgnoreCase = {item.IgnoreCase} AND
Regex = '{SqlSafeCodec.Encode(item.Regex)}'";
            Engine.LocalDB.ExecuteNonQuery(sql);
        }

        public static PageItem<List<AdvancedDictionaryItem>> QueryByPage(int From, int To, int PageNo)
        {
            string Where = $"WHERE [From] = {From} And [To] = {To}";

            int MaxPage = PageHelper.GetPageCount("AdvancedDictionary", Where);

            List<Dictionary<string, object>> NTable = PageHelper.GetTablePageData("AdvancedDictionary", PageNo, EngineConfig.DefPageSize, Where);

            List<AdvancedDictionaryItem> Items = new List<AdvancedDictionaryItem>();
            for (int i = 0; i < NTable.Count; i++)
            {
                var Row = NTable[i]; // row 是 Dictionary<string, object>

                Items.Add(new AdvancedDictionaryItem(
                    Row["Rowid"],
                    SqlSafeCodec.Decode(ConvertHelper.ObjToStr(Row["TargetFileName"])),
                    SqlSafeCodec.Decode(ConvertHelper.ObjToStr(Row["Type"])),
                    SqlSafeCodec.Decode(ConvertHelper.ObjToStr(Row["Source"])),
                    SqlSafeCodec.Decode(ConvertHelper.ObjToStr(Row["Result"])),
                    Row["From"],
                    Row["To"],
                    Row["ExactMatch"],
                    Row["IgnoreCase"],
                    SqlSafeCodec.Decode(ConvertHelper.ObjToStr(Row["Regex"]))
                ));
            }

            return new PageItem<List<AdvancedDictionaryItem>>(Items, PageNo, MaxPage);
        }

        public static PageItem<List<AdvancedDictionaryItem>> QueryByPage(string SourceText,int From,int To, int PageNo)
        {
            string Where = $"WHERE Source = '{SqlSafeCodec.Encode(SourceText)}' And [From] = {From} And [To] = {To}";

            int MaxPage = PageHelper.GetPageCount("AdvancedDictionary", Where);

            List<Dictionary<string, object>> NTable = PageHelper.GetTablePageData("AdvancedDictionary", PageNo, EngineConfig.DefPageSize, Where);

            List<AdvancedDictionaryItem> Items = new List<AdvancedDictionaryItem>();
            for (int i = 0; i < NTable.Count; i++)
            {
                var Row = NTable[i];

                Items.Add(new AdvancedDictionaryItem(
                    SqlSafeCodec.Decode(ConvertHelper.ObjToStr(Row["TargetFileName"])),
                    SqlSafeCodec.Decode(ConvertHelper.ObjToStr(Row["Type"])),
                    SqlSafeCodec.Decode(ConvertHelper.ObjToStr(Row["Source"])),
                    SqlSafeCodec.Decode(ConvertHelper.ObjToStr(Row["Result"])),
                    Row["From"],
                    Row["To"],
                    Row["ExactMatch"],
                    Row["IgnoreCase"],
                    SqlSafeCodec.Decode(ConvertHelper.ObjToStr(Row["Regex"]))
                ));
            }

            return new PageItem<List<AdvancedDictionaryItem>>(Items, PageNo, MaxPage);
        }

        public static bool DeleteByRowid(int Rowid)
        {
            string SqlOrder = "Delete From AdvancedDictionary Where Rowid = {0}";
            int State = Engine.LocalDB.ExecuteNonQuery(string.Format(SqlOrder,Rowid));
            if (State != 0)
            {
                return true;
            }
            return false;
        }

    }
}
