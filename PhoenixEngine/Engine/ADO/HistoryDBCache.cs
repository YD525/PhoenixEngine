using System;
using System.Collections.Generic;
using System.Linq;
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
            string SqlOrder = string.Format("Select [Keys] From [RecordsHistory] Where [FileUniqueKey] = {0} And [IsCurrent] = 1",FileUniqueKey);

            var Result = P_Convert.ObjToStr(Phoenix.LocalDB.ExecuteScalar(SqlOrder));

            return Result;
        }
    }
}
