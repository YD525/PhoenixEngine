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
        public List<string> Keys = new List<string>();
        public int To = 0;
        public string PreviousText = "";
        public string CurrentText = "";
        public int IsCurrent = 0;
        public DateTime Time;

        public HistoryItem(object FileUniqueKey, object Keys, object To, object PreviousText, object CurrentText, object IsCurrent, object Time)
        {
            this.FileUniqueKey = P_Convert.ObjToInt(FileUniqueKey);

            string GetKeysArray = P_Convert.ObjToStr(Keys);

            foreach (var Key in GetKeysArray.Split(','))
            {
                string TrimKey = Key.Trim();

                if (TrimKey.Length > 0)
                {
                    this.Keys.Add(TrimKey);
                }
            }

            this.To = P_Convert.ObjToInt(To);

            this.PreviousText = P_Convert.ObjToStr(PreviousText);
            this.CurrentText = P_Convert.ObjToStr(CurrentText);

            this.IsCurrent = P_Convert.ObjToInt(IsCurrent);

            this.Time = TimeHelper.TimestampToDateTime(P_Convert.ObjToLong(Time));
        }

        public string GetKeysStr()
        {
            string MergeKey = "";
            foreach (var Key in this.Keys)
            {
                string TrimKey = Key.Trim();
                if (TrimKey.Length > 0)
                {
                    MergeKey += TrimKey + ",";
                }
            }
            if (MergeKey.EndsWith(","))
            {
                MergeKey = MergeKey.Substring(0, MergeKey.Length - ",".Length);
            }
            return MergeKey;
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
    [Keys] TEXT,
    [To] INT,
    [PreviousText] TEXT,
    [CurrentText] TEXT,
    [IsCurrent] INT,
    [Time] INT64
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
                "Keys",
                "To",
                "PreviousText",
                "CurrentText",
                "IsCurrent",
                "Time"
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

        //Ctrl+Z

        public string GetPreviousKey(int FileUniqueKey, string CurrentKeys)
        {
            string SqlOrder = $@"
SELECT [Keys]
FROM [RecordsHistory]
WHERE [FileUniqueKey] = {FileUniqueKey}
AND [Time] < 
(
    SELECT [Time]
    FROM [RecordsHistory]
    WHERE [FileUniqueKey] = {FileUniqueKey}
    AND [Keys] = '{CurrentKeys}'
    ORDER BY [Time] DESC
    LIMIT 1
)
ORDER BY [Time] DESC
LIMIT 1;
";

            return P_Convert.ObjToStr(
                Phoenix.LocalDB.ExecuteScalar(SqlOrder)
            );
        }

        //Ctrl+Y

        public string GetNextKey(int FileUniqueKey, string CurrentKeys)
        {
            string SqlOrder = $@"
SELECT [Keys]
FROM [RecordsHistory]
WHERE [FileUniqueKey] = {FileUniqueKey}
AND [Time] >
(
    SELECT [Time]
    FROM [RecordsHistory]
    WHERE [FileUniqueKey] = {FileUniqueKey}
    AND [Keys] = '{CurrentKeys}'
    ORDER BY [Time] DESC
    LIMIT 1
)
ORDER BY [Time] ASC
LIMIT 1;
";

            return P_Convert.ObjToStr(
                Phoenix.LocalDB.ExecuteScalar(SqlOrder)
            );
        }

        //Click
        public void SelectKey(int FileUniqueKey, string CurrentKeys)
        {
            // Clear Current flag
            string SqlOrder = $@"UPDATE [RecordsHistory] SET [IsCurrent] = 0 WHERE [FileUniqueKey] = {FileUniqueKey};";

            Phoenix.LocalDB.ExecuteNonQuery(SqlOrder);

            // Set Selected history as current
            SqlOrder = $@"UPDATE [RecordsHistory] SET [IsCurrent] = 1 WHERE [FileUniqueKey] = {FileUniqueKey} AND [Keys] = '{CurrentKeys}';";

            Phoenix.LocalDB.ExecuteNonQuery(SqlOrder);
        }

        //Get Current Key
        public string GetSelectKey(int FileUniqueKey)
        {
            string SqlOrder = string.Format("Select [Keys] From [RecordsHistory] Where [FileUniqueKey] = {0} And [IsCurrent] = 1", FileUniqueKey);

            var Result = P_Convert.ObjToStr(Phoenix.LocalDB.ExecuteScalar(SqlOrder));

            return Result;
        }

        //Add
        public bool AddHistory(HistoryItem Item)
        {
            string SqlOrder = "Insert Into RecordsHistory(FileUniqueKey,Keys,To,PreviousText,CurrentText,Time)Values({0},'{1}',{2},'{3}','{4}',{5})";
            int State = Phoenix.LocalDB.ExecuteNonQuery(string.Format(SqlOrder,
                Item.FileUniqueKey,
                Item.GetKeysStr(),
                Item.To,
                SQLSafeCodec.Encode(Item.PreviousText),
                SQLSafeCodec.Encode(Item.CurrentText),
                TimeHelper.DateTimeToTimestamp(Item.Time)));
            if (State != 0)
            {
                return true;
            }
            return false;
        }

        //Delete
        public bool DeleteHistory(int FileUniqueKey, string Keys)
        {
            string SqlOrder = "Delete From RecordsHistory Where FileUniqueKey = {0} And Keys = '{1}'";

            int State = Phoenix.LocalDB.ExecuteNonQuery(string.Format(SqlOrder, FileUniqueKey, Keys));

            if (State != 0)
            {
                return true;
            }

            return false;
        }

        //Get Full InFo By CurrentKeys
        public HistoryItem KeysToHistoryItem(int FileUniqueKey, string Keys)
        {
            string SqlOrder = "Select * From RecordsHistory Where FileUniqueKey = {0} And Keys = '{1}' Limit = 1";

            var NTable = Phoenix.LocalDB.ExecuteQuery(string.Format(SqlOrder, FileUniqueKey, Keys));

            if (NTable.Count > 0)
            {
                var Row = NTable[0];

                return new HistoryItem(
                    Row["FileUniqueKey"],
                    Row["Keys"],
                    Row["To"],
                    SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["PreviousText"])),
                    SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["CurrentText"])),
                    Row["IsCurrent"],
                    Row["Time"]
                );
            }

            return null;
        }

        //Get HistoryItems
        public List<HistoryItem> GetHistoryItems(int FileUniqueKey)
        {
            List<HistoryItem> HistoryItems = new List<HistoryItem>();

            string SqlOrder = "Select * From RecordsHistory Where FileUniqueKey = {0}";

            var NTable = Phoenix.LocalDB.ExecuteQuery(string.Format(SqlOrder, FileUniqueKey));

            if (NTable.Count > 0)
            {
                for (int i = 0; i < NTable.Count; i++)
                {
                    var Row = NTable[i];

                    HistoryItems.Add(new HistoryItem(
                        Row["FileUniqueKey"],
                        Row["Keys"],
                        Row["To"],
                        SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["PreviousText"])),
                        SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["CurrentText"])),
                        Row["IsCurrent"],
                        Row["Time"]
                    ));
                }
            }

            return HistoryItems;
        }
    }
}
