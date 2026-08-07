using System;
using System.Collections.Generic;
using System.Linq;

namespace PhoenixEngine.Engine.ADO
{
    public class HistoryDBCache
    {
        public static void Init()
        {
            string TableName = "RecordsHistory";

            string CreateSql = @"
CREATE TABLE [RecordsHistory](
    [FileUniqueKey] TEXT,
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
    }
}
