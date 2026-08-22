using System;
using System.Collections.Generic;
using System.Linq;
using PhoenixEngine.ADO;
using PhoenixEngine.Common;
using PhoenixEngine.Platform.Request;

namespace PhoenixEngine.Engine.ADO
{
    public class HistoryItem
    {
        public int FileUniqueKey = 0;
        public int Rowid = 0;
        public string Key = "";
        public int To = 0;
        public string CurrentText = "";
        public int IsCurrent = 0;
        public DateTime Time;
        public string RangeID = "";

        public HistoryItem(object FileUniqueKey, object Rowid, object Key, object To,object CurrentText, object IsCurrent, object Time, object RangeID)
        {
            this.FileUniqueKey = P_Convert.ObjToInt(FileUniqueKey);

            this.Rowid = P_Convert.ObjToInt(Rowid);
            this.Key = P_Convert.ObjToStr(Key);

            this.To = P_Convert.ObjToInt(To);

            this.CurrentText = P_Convert.ObjToStr(CurrentText);

            this.IsCurrent = P_Convert.ObjToInt(IsCurrent);

            this.Time = TimeHelper.TimestampToDateTime(P_Convert.ObjToLong(Time));

            this.RangeID = P_Convert.ObjToStr(RangeID);
        }

        public HistoryItem(int FileUniqueKey, string Key, int To, string CurrentText, int IsCurrent, DateTime Time, string RangeID)
        {
            this.FileUniqueKey = FileUniqueKey;

            this.Key = Key;

            this.To = To;

            this.CurrentText = CurrentText;

            this.IsCurrent = IsCurrent;

            this.Time = Time;

            this.RangeID = RangeID;
        }
    }
    public class HistoryDBCache
    {
        public static void Init()
        {
            string TableName = "RecordsHistory";

            string CreateSql = @"
CREATE TABLE [RecordsHistory](
    [FileUniqueKey] INT,
    [Key] TEXT,
    [To] INT,
    [CurrentText] TEXT,
    [IsCurrent] INT,
    [Time] INT64,
    [RangeID] TEXT
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

                string[] ExpectedCols =
                {
                "FileUniqueKey",
                "Key",
                "To",
                "CurrentText",
                "IsCurrent",
                "Time",
                "RangeID"
                };

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

        private static object LockGenRangeID = new object();
        public static string GenRangeID()
        {
            lock (LockGenRangeID)
            {
                return DateTime.UtcNow.Ticks + "_" + new Random(Guid.NewGuid().GetHashCode()).Next(1000, 9999);
            }
        }

        //Ctrl+Z
        public static List<int> GetPreviousIDs(int FileUniqueKey, int CurrentID)
        {
            List<int> IDs = new List<int>();

            const string SqlOrder = @"
SELECT rowid AS Rowid, [RangeID], [Key]
FROM [RecordsHistory]
WHERE [FileUniqueKey] = @fileUniqueKey
AND rowid < @currentId
ORDER BY rowid DESC
LIMIT 1;
";

            var Table = Phoenix.LocalDB.ExecuteQuery(
                SqlOrder,
                SqliteSql.Parameter("@fileUniqueKey", FileUniqueKey),
                SqliteSql.Parameter("@currentId", CurrentID));

            if (Table.Count == 0)
                return IDs;

            string RangeID = P_Convert.ObjToStr(Table[0]["RangeID"]);

            if (string.IsNullOrEmpty(RangeID))
            {
                IDs.Add(P_Convert.ObjToInt(Table[0]["Rowid"]));
            }
            else
            {
                const string RangeSql = @"
SELECT rowid AS Rowid
FROM [RecordsHistory]
WHERE [FileUniqueKey] = @fileUniqueKey
AND [RangeID] = @rangeId
ORDER BY rowid ASC;";
                var RangeTable = Phoenix.LocalDB.ExecuteQuery(
                    RangeSql,
                    SqliteSql.Parameter("@fileUniqueKey", FileUniqueKey),
                    SqliteSql.Parameter("@rangeId", RangeID));
                foreach (var Row in RangeTable)
                {
                    IDs.Add(P_Convert.ObjToInt(Row["Rowid"]));
                }
            }

            return IDs;
        }

        //Ctrl+Y
        public static List<int> GetNextIDs(int FileUniqueKey, int CurrentID)
        {
            List<int> IDs = new List<int>();

            const string SqlOrder = @"
SELECT rowid AS Rowid, [RangeID], [Key]
FROM [RecordsHistory]
WHERE [FileUniqueKey] = @fileUniqueKey
AND rowid > @currentId
ORDER BY rowid ASC
LIMIT 1;
";

            var Table = Phoenix.LocalDB.ExecuteQuery(
                SqlOrder,
                SqliteSql.Parameter("@fileUniqueKey", FileUniqueKey),
                SqliteSql.Parameter("@currentId", CurrentID));

            if (Table.Count == 0)
                return IDs;

            string RangeID = P_Convert.ObjToStr(Table[0]["RangeID"]);

            if (string.IsNullOrEmpty(RangeID))
            {
                IDs.Add(P_Convert.ObjToInt(Table[0]["Rowid"]));
            }
            else
            {
                const string RangeSql = @"
SELECT rowid AS Rowid
FROM [RecordsHistory]
WHERE [FileUniqueKey] = @fileUniqueKey
AND [RangeID] = @rangeId
ORDER BY rowid ASC;";
                var RangeTable = Phoenix.LocalDB.ExecuteQuery(
                    RangeSql,
                    SqliteSql.Parameter("@fileUniqueKey", FileUniqueKey),
                    SqliteSql.Parameter("@rangeId", RangeID));
                foreach (var Row in RangeTable)
                {
                    IDs.Add(P_Convert.ObjToInt(Row["Rowid"]));
                }
            }

            return IDs;
        }

        //Set Pointer
        public static void SelectID(int FileUniqueKey, int ID)
        {
            const string ClearSelectionSql = @"
UPDATE [RecordsHistory]
SET [IsCurrent] = 0
WHERE [FileUniqueKey] = @fileUniqueKey;
";
            Phoenix.LocalDB.ExecuteNonQuery(
                ClearSelectionSql,
                SqliteSql.Parameter("@fileUniqueKey", FileUniqueKey));

            const string FindRangeSql = @"
SELECT [RangeID]
FROM [RecordsHistory]
WHERE [FileUniqueKey] = @fileUniqueKey
AND rowid = @rowid
LIMIT 1;
";

            string RangeID = P_Convert.ObjToStr(Phoenix.LocalDB.ExecuteScalar(
                FindRangeSql,
                SqliteSql.Parameter("@fileUniqueKey", FileUniqueKey),
                SqliteSql.Parameter("@rowid", ID)));

            if (!string.IsNullOrEmpty(RangeID))
            {
                const string SelectRangeSql = @"
UPDATE [RecordsHistory]
SET [IsCurrent] = 1
WHERE [FileUniqueKey] = @fileUniqueKey
AND [RangeID] = @rangeId;
";
                Phoenix.LocalDB.ExecuteNonQuery(
                    SelectRangeSql,
                    SqliteSql.Parameter("@fileUniqueKey", FileUniqueKey),
                    SqliteSql.Parameter("@rangeId", RangeID));
            }
            else
            {
                const string SelectRowSql = @"
UPDATE [RecordsHistory]
SET [IsCurrent] = 1
WHERE [FileUniqueKey] = @fileUniqueKey
AND rowid = @rowid;
";
                Phoenix.LocalDB.ExecuteNonQuery(
                    SelectRowSql,
                    SqliteSql.Parameter("@fileUniqueKey", FileUniqueKey),
                    SqliteSql.Parameter("@rowid", ID));
            }
        }

        public static List<int> GetSelectIDs(int FileUniqueKey)
        {
            List<int> IDs = new List<int>();

            const string SqlOrder = @"
SELECT rowid AS Rowid
FROM [RecordsHistory]
WHERE [FileUniqueKey] = @fileUniqueKey
AND [IsCurrent] = 1;
";

            var Table = Phoenix.LocalDB.ExecuteQuery(
                SqlOrder,
                SqliteSql.Parameter("@fileUniqueKey", FileUniqueKey));
            foreach (var Row in Table)
            {
                IDs.Add(P_Convert.ObjToInt(Row["Rowid"]));
            }

            return IDs;
        }

        //Add
        public static int AddHistory(HistoryItem Item)
        {
            const string SqlOrder = @"
INSERT INTO RecordsHistory (FileUniqueKey, [Key], [To], CurrentText, [Time], RangeID)
VALUES (@fileUniqueKey, @key, @to, @currentText, @time, @rangeId)
RETURNING rowid;";
            int Rowid = P_Convert.ObjToInt(Phoenix.LocalDB.ExecuteScalar(
                SqlOrder,
                SqliteSql.Parameter("@fileUniqueKey", Item.FileUniqueKey),
                SqliteSql.Parameter("@key", Item.Key),
                SqliteSql.Parameter("@to", Item.To),
                SqliteSql.Parameter("@currentText", SQLSafeCodec.Encode(Item.CurrentText)),
                SqliteSql.Parameter("@time", TimeHelper.DateTimeToTimestamp(Item.Time)),
                SqliteSql.Parameter("@rangeId", Item.RangeID)));
            return Rowid;
        }


        //Delete
        public static bool DeleteHistory(int FileUniqueKey, int ID)
        {
            const string SqlOrder = @"
DELETE FROM RecordsHistory
WHERE FileUniqueKey = @fileUniqueKey AND rowid = @rowid;";
            int State = Phoenix.LocalDB.ExecuteNonQuery(
                SqlOrder,
                SqliteSql.Parameter("@fileUniqueKey", FileUniqueKey),
                SqliteSql.Parameter("@rowid", ID));
            return State != 0;
        }

        public static bool CheckPreviousHistoryItem(
     int CurrentID,
     int FileUniqueKey,
     int To,
     string Key,
     string CurrentText,
     out int TargetID)
        {
            int PreviousRowid = P_Convert.ObjToInt(
                Phoenix.LocalDB.ExecuteScalar(@"
SELECT Rowid
FROM RecordsHistory
WHERE Rowid < @currentId
ORDER BY Rowid DESC
LIMIT 1;
",
                    SqliteSql.Parameter("@currentId", CurrentID))
            );


            if (PreviousRowid <= 0)
            {
                TargetID = 0;
                return false;
            }

            int Count = P_Convert.ObjToInt(
                Phoenix.LocalDB.ExecuteScalar(@"
SELECT COUNT(*)
FROM RecordsHistory
WHERE Rowid = @rowid
AND FileUniqueKey = @fileUniqueKey
AND [To] = @to
AND [Key] = @key
AND CurrentText = @currentText;
",
                    SqliteSql.Parameter("@rowid", PreviousRowid),
                    SqliteSql.Parameter("@fileUniqueKey", FileUniqueKey),
                    SqliteSql.Parameter("@to", To),
                    SqliteSql.Parameter("@key", Key),
                    SqliteSql.Parameter("@currentText", SQLSafeCodec.Encode(CurrentText)))
            );

            if (Count > 0)
            {
                TargetID = PreviousRowid;

                return true;
            }
            else
            {
                TargetID = 0;
                return false;
            }
        }

        //Get Full InFo By CurrentKey
        public static HistoryItem IDToHistoryItem(int FileUniqueKey, int ID)
        {
            const string SqlOrder = @"
SELECT rowid AS Rowid, * FROM RecordsHistory
WHERE FileUniqueKey = @fileUniqueKey AND rowid = @rowid
LIMIT 1;";
            var NTable = Phoenix.LocalDB.ExecuteQuery(
                SqlOrder,
                SqliteSql.Parameter("@fileUniqueKey", FileUniqueKey),
                SqliteSql.Parameter("@rowid", ID));

            if (NTable.Count > 0)
            {
                var Row = NTable[0];
                return new HistoryItem(
                    Row["FileUniqueKey"],
                    Row["Rowid"],
                    Row["Key"],
                    Row["To"],
                    SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["CurrentText"])),
                    Row["IsCurrent"],
                    Row["Time"],
                    Row["RangeID"]
                );
            }
            return null;
        }

        //Get HistoryItems
        public static List<HistoryItem> GetHistoryItems(int FileUniqueKey,int To)
        {
            List<HistoryItem> HistoryItems = new List<HistoryItem>();

            const string SqlOrder = @"
SELECT rowid AS Rowid, * FROM RecordsHistory
WHERE FileUniqueKey = @fileUniqueKey AND [To] = @to;";

            var NTable = Phoenix.LocalDB.ExecuteQuery(
                SqlOrder,
                SqliteSql.Parameter("@fileUniqueKey", FileUniqueKey),
                SqliteSql.Parameter("@to", To));

            if (NTable.Count > 0)
            {
                for (int i = 0; i < NTable.Count; i++)
                {
                    var Row = NTable[i];

                    HistoryItems.Add(new HistoryItem(
                        Row["FileUniqueKey"],
                        Row["Rowid"],
                        Row["Key"],
                        Row["To"],
                        SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["CurrentText"])),
                        Row["IsCurrent"],
                        Row["Time"],
                        Row["RangeID"]
                    ));
                }
            }

            return HistoryItems;
        }

        public static bool ClearHistory(int FileUniqueKey)
        {
            const string SqlOrder =
                "DELETE FROM RecordsHistory WHERE FileUniqueKey = @fileUniqueKey;";

            int State = Phoenix.LocalDB.ExecuteNonQuery(
                SqlOrder,
                SqliteSql.Parameter("@fileUniqueKey", FileUniqueKey));

            if (State != 0)
            {
                return true;
            }
            return false;
        }

        // Compact history records.
        // Removes old history entries when the record count exceeds the limit.
        // RangeID records are treated as a single operation and will not be split.
        public static bool CompactHistory(int FileUniqueKey, int MaxCount = 10000)
        {
            const string CountSql = @"
SELECT COUNT(*) 
FROM RecordsHistory
WHERE FileUniqueKey = @fileUniqueKey;
";

            int Count = P_Convert.ObjToInt(
                Phoenix.LocalDB.ExecuteScalar(
                    CountSql,
                    SqliteSql.Parameter("@fileUniqueKey", FileUniqueKey))
            );

            if (Count <= MaxCount)
                return false;

            int NeedDelete = Count - MaxCount;

            while (NeedDelete > 0)
            {
                const string FindSql = @"
SELECT rowid AS Rowid, [RangeID]
FROM RecordsHistory
WHERE FileUniqueKey = @fileUniqueKey
ORDER BY rowid ASC
LIMIT 1;
";

                var Table = Phoenix.LocalDB.ExecuteQuery(
                    FindSql,
                    SqliteSql.Parameter("@fileUniqueKey", FileUniqueKey));

                if (Table.Count == 0)
                    break;

                string RangeID = P_Convert.ObjToStr(Table[0]["RangeID"]);
                int DeleteCount = 0;

                if (!string.IsNullOrEmpty(RangeID))
                {
                    const string DeleteRangeSql = @"
DELETE FROM RecordsHistory
WHERE FileUniqueKey = @fileUniqueKey
AND RangeID = @rangeId;
";
                    DeleteCount = Phoenix.LocalDB.ExecuteNonQuery(
                        DeleteRangeSql,
                        SqliteSql.Parameter("@fileUniqueKey", FileUniqueKey),
                        SqliteSql.Parameter("@rangeId", RangeID));
                }
                else
                {
                    int Rowid = P_Convert.ObjToInt(Table[0]["Rowid"]);
                    const string DeleteSql = @"
DELETE FROM RecordsHistory
WHERE rowid = @rowid;
";
                    DeleteCount = Phoenix.LocalDB.ExecuteNonQuery(
                        DeleteSql,
                        SqliteSql.Parameter("@rowid", Rowid));
                }

                if (DeleteCount <= 0)
                    break;

                NeedDelete -= DeleteCount;
            }

            return true;
        }

        public static int GetLastID(int FileUniqueKey)
        {
            const string SqlOrder = @"
SELECT rowid AS Rowid
FROM [RecordsHistory]
WHERE [FileUniqueKey] = @fileUniqueKey
ORDER BY rowid DESC
LIMIT 1;
";

            var Table = Phoenix.LocalDB.ExecuteQuery(
                SqlOrder,
                SqliteSql.Parameter("@fileUniqueKey", FileUniqueKey));

            if (Table.Count > 0)
            {
                return P_Convert.ObjToInt(Table[0]["Rowid"]);
            }

            return 0;
        }
    }
}
