using System;
using System.Collections.Generic;
using System.Linq;
using PhoenixEngine.Common;

namespace PhoenixEngine.Engine.ADO
{
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
            string Sql = $@"
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
                Phoenix.LocalDB.ExecuteScalar(Sql)
            );
        }

        //Ctrl+Y

        public string GetNextKey(int FileUniqueKey, string CurrentKeys)
        {
            string Sql = $@"
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
                Phoenix.LocalDB.ExecuteScalar(Sql)
            );
        }
    }
}
