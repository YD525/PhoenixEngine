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
        public string PreviousText = "";
        public string CurrentText = "";
        public int IsCurrent = 0;
        public DateTime Time;
        public string RangeID = "";

        public HistoryItem(object FileUniqueKey,object Rowid, object Key, object To, object PreviousText, object CurrentText, object IsCurrent, object Time,object RangeID)
        {
            this.FileUniqueKey = P_Convert.ObjToInt(FileUniqueKey);

            this.Rowid = P_Convert.ObjToInt(Rowid);
            this.Key = P_Convert.ObjToStr(Key);

            this.To = P_Convert.ObjToInt(To);

            this.PreviousText = P_Convert.ObjToStr(PreviousText);
            this.CurrentText = P_Convert.ObjToStr(CurrentText);

            this.IsCurrent = P_Convert.ObjToInt(IsCurrent);

            this.Time = TimeHelper.TimestampToDateTime(P_Convert.ObjToLong(Time));

            this.RangeID = P_Convert.ObjToStr(RangeID);
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
    [PreviousText] TEXT,
    [CurrentText] TEXT,
    [IsCurrent] INT,
    [Time] INT64,
    [RangeID] TEXT
);";

            // Check if table exists
            string CheckTableSql = $"SELECT name FROM sqlite_master WHERE type='table' AND name='{TableName}';";
            var Result = Phoenix.LocalDB.ExecuteScalar(CheckTableSql);

            if (Result != null && Result != DBNull.Value)
            {
                // Table exists, check structure
                List<Dictionary<string, object>> Columns =
                    Phoenix.LocalDB.ExecuteQuery($"PRAGMA table_info([{TableName}]);");

                var ExistingCols = new HashSet<string>(
                    Columns.Select(R => R["name"].ToString()),
                    StringComparer.OrdinalIgnoreCase
                );

                string[] ExpectedCols =
                {
                "FileUniqueKey",
                "Key",
                "To",
                "PreviousText",
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
                    Phoenix.LocalDB.ExecuteNonQuery($"DROP TABLE IF EXISTS [{TableName}];");
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
        public static List<int> GetPreviousRowid(int FileUniqueKey, int CurrentRowid)
        {
            List<int> Rowids = new List<int>();

            string SqlOrder = $@"
SELECT rowid, [RangeID], [Key]
FROM [RecordsHistory]
WHERE [FileUniqueKey] = {FileUniqueKey}
AND rowid < {CurrentRowid}
ORDER BY rowid DESC
LIMIT 1;
";

            var Table = Phoenix.LocalDB.ExecuteQuery(SqlOrder);

            if (Table.Count == 0)
                return Rowids;

            string RangeID = P_Convert.ObjToStr(Table[0]["RangeID"]);

            if (string.IsNullOrEmpty(RangeID))
            {
                Rowids.Add(P_Convert.ObjToInt(Table[0]["rowid"]));
            }
            else
            {
                string RangeSql = $@"
SELECT rowid
FROM [RecordsHistory]
WHERE [FileUniqueKey] = {FileUniqueKey}
AND [RangeID] = '{RangeID}';
";
                var RangeTable = Phoenix.LocalDB.ExecuteQuery(RangeSql);
                foreach (var Row in RangeTable)
                {
                    Rowids.Add(P_Convert.ObjToInt(Row["rowid"]));
                }
            }

            return Rowids;
        }

        //Ctrl+Y
        public static List<int> GetNextRowid(int FileUniqueKey, int CurrentRowid)
        {
            List<int> Rowids = new List<int>();

            string SqlOrder = $@"
SELECT rowid, [RangeID], [Key]
FROM [RecordsHistory]
WHERE [FileUniqueKey] = {FileUniqueKey}
AND rowid > {CurrentRowid}
ORDER BY rowid ASC
LIMIT 1;
";

            var Table = Phoenix.LocalDB.ExecuteQuery(SqlOrder);

            if (Table.Count == 0)
                return Rowids;

            string RangeID = P_Convert.ObjToStr(Table[0]["RangeID"]);

            if (string.IsNullOrEmpty(RangeID))
            {
                Rowids.Add(P_Convert.ObjToInt(Table[0]["rowid"]));
            }
            else
            {
                string RangeSql = $@"
SELECT rowid
FROM [RecordsHistory]
WHERE [FileUniqueKey] = {FileUniqueKey}
AND [RangeID] = '{RangeID}';
";
                var RangeTable = Phoenix.LocalDB.ExecuteQuery(RangeSql);
                foreach (var Row in RangeTable)
                {
                    Rowids.Add(P_Convert.ObjToInt(Row["rowid"]));
                }
            }

            return Rowids;
        }

        //Set Pointer
        public static void SelectRowid(int FileUniqueKey, int Rowid)
        {
            string SqlOrder = $@"
UPDATE [RecordsHistory]
SET [IsCurrent] = 0
WHERE [FileUniqueKey] = {FileUniqueKey};
";
            Phoenix.LocalDB.ExecuteNonQuery(SqlOrder);

            SqlOrder = $@"
SELECT [RangeID]
FROM [RecordsHistory]
WHERE [FileUniqueKey] = {FileUniqueKey}
AND [rowid] = {Rowid}
LIMIT 1;
";

            string RangeID = P_Convert.ObjToStr(Phoenix.LocalDB.ExecuteScalar(SqlOrder));

            if (!string.IsNullOrEmpty(RangeID))
            {
                SqlOrder = $@"
UPDATE [RecordsHistory]
SET [IsCurrent] = 1
WHERE [FileUniqueKey] = {FileUniqueKey}
AND [RangeID] = '{RangeID}';
";
            }
            else
            {
                SqlOrder = $@"
UPDATE [RecordsHistory]
SET [IsCurrent] = 1
WHERE [FileUniqueKey] = {FileUniqueKey}
AND [rowid] = {Rowid};
";
            }

            Phoenix.LocalDB.ExecuteNonQuery(SqlOrder);
        }

        public static List<int> GetSelectRowids(int FileUniqueKey)
        {
            List<int> Rowids = new List<int>();

            string SqlOrder = $@"
SELECT rowid
FROM [RecordsHistory]
WHERE [FileUniqueKey] = {FileUniqueKey}
AND [IsCurrent] = 1;
";

            var Table = Phoenix.LocalDB.ExecuteQuery(SqlOrder);
            foreach (var Row in Table)
            {
                Rowids.Add(P_Convert.ObjToInt(Row["rowid"]));
            }

            return Rowids;
        }

        //Add
        public static bool AddHistory(HistoryItem Item)
        {
            string SqlOrder = "Insert Into RecordsHistory(FileUniqueKey,[Key],[To],PreviousText,CurrentText,[Time],RangeID)Values({0},'{1}',{2},'{3}','{4}',{5},'{6}')";
            int State = Phoenix.LocalDB.ExecuteNonQuery(string.Format(SqlOrder,
                Item.FileUniqueKey,
                Item.Key,
                Item.To,
                SQLSafeCodec.Encode(Item.PreviousText),
                SQLSafeCodec.Encode(Item.CurrentText),
                TimeHelper.DateTimeToTimestamp(Item.Time),
                Item.RangeID
                ));
            if (State != 0)
            {
                return true;
            }
            return false;
        }


        //Delete
        public static bool DeleteHistory(int FileUniqueKey, int Rowid)
        {
            string SqlOrder = "Delete From RecordsHistory Where FileUniqueKey = {0} And rowid = {1}";
            int State = Phoenix.LocalDB.ExecuteNonQuery(string.Format(SqlOrder, FileUniqueKey, Rowid));
            return State != 0;
        }

        //Get Full InFo By CurrentKey
        public static HistoryItem RowidToHistoryItem(int FileUniqueKey, int Rowid)
        {
            string SqlOrder = "Select Rowid,* From RecordsHistory Where FileUniqueKey = {0} And rowid = {1} Limit 1";
            var NTable = Phoenix.LocalDB.ExecuteQuery(string.Format(SqlOrder, FileUniqueKey, Rowid));

            if (NTable.Count > 0)
            {
                var Row = NTable[0];
                return new HistoryItem(
                    Row["FileUniqueKey"],
                    Row["Rowid"],
                    Row["Key"],
                    Row["To"],
                    SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["PreviousText"])),
                    SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["CurrentText"])),
                    Row["IsCurrent"],
                    Row["Time"],
                    Row["RangeID"]
                );
            }
            return null;
        }

        //Get HistoryItems
        public static List<HistoryItem> GetHistoryItems(int FileUniqueKey)
        {
            List<HistoryItem> HistoryItems = new List<HistoryItem>();

            string SqlOrder = "Select Rowid,* From RecordsHistory Where FileUniqueKey = {0}";

            var NTable = Phoenix.LocalDB.ExecuteQuery(string.Format(SqlOrder, FileUniqueKey));

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
                        SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["PreviousText"])),
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
            string SqlOrder = "Delete From RecordsHistory Where FileUniqueKey = {0}";

            int State = Phoenix.LocalDB.ExecuteNonQuery(string.Format(SqlOrder,FileUniqueKey));

            if (State!=0)
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
            string CountSql = $@"
SELECT COUNT(*) 
FROM RecordsHistory
WHERE FileUniqueKey = {FileUniqueKey};
";

            int Count = P_Convert.ObjToInt(
                Phoenix.LocalDB.ExecuteScalar(CountSql)
            );

            if (Count <= MaxCount)
                return false;

            int NeedDelete = Count - MaxCount;

            while (NeedDelete > 0)
            {
                string FindSql = $@"
SELECT rowid, [RangeID]
FROM RecordsHistory
WHERE FileUniqueKey = {FileUniqueKey}
ORDER BY rowid ASC
LIMIT 1;
";

                var Table = Phoenix.LocalDB.ExecuteQuery(FindSql);

                if (Table.Count == 0)
                    break;

                string RangeID = P_Convert.ObjToStr(Table[0]["RangeID"]);
                int DeleteCount = 0;

                if (!string.IsNullOrEmpty(RangeID))
                {
                    string DeleteRangeSql = $@"
DELETE FROM RecordsHistory
WHERE FileUniqueKey = {FileUniqueKey}
AND RangeID = '{RangeID}';
";
                    DeleteCount = Phoenix.LocalDB.ExecuteNonQuery(DeleteRangeSql);
                }
                else
                {
                    int Rowid = P_Convert.ObjToInt(Table[0]["rowid"]);
                    string DeleteSql = $@"
DELETE FROM RecordsHistory
WHERE rowid = {Rowid};
";
                    DeleteCount = Phoenix.LocalDB.ExecuteNonQuery(DeleteSql);
                }

                if (DeleteCount <= 0)
                    break;

                NeedDelete -= DeleteCount;
            }

            return true;
        }

        public static int GetLastRowid(int FileUniqueKey)
        {
            string SqlOrder = $@"
SELECT [Rowid]
FROM [RecordsHistory]
WHERE [FileUniqueKey] = {FileUniqueKey}
ORDER BY rowid DESC
LIMIT 1;
";

            var Table = Phoenix.LocalDB.ExecuteQuery(SqlOrder);

            if (Table.Count > 0)
            {
                return P_Convert.ObjToInt(Table[0]["Rowid"]);
            }

            return 0;
        }
    }   
}
