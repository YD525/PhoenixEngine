using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using PhoenixEngine.ADO;
using PhoenixEngine.Common;

namespace PhoenixEngine.Additional
{
    public class FontColorFinder
    {
        public class FontColor
        {
            public int FileUniqueKey = 0;
            public string Key = "";
            public int R = 0;
            public int G = 0;
            public int B = 0;

            public FontColor(int FileUniqueKey, string Key, int R, int G, int B)
            { 
               this.FileUniqueKey = FileUniqueKey;
               this.Key = Key;
               this.R = R;
               this.G = G;
               this.B = B;
            }

            public FontColor(object FileUniqueKey, object Key, object R, object G, object B)
            {
                this.FileUniqueKey = P_Convert.ObjToInt(FileUniqueKey);
                this.Key = P_Convert.ObjToStr(Key);
                this.R = P_Convert.ObjToInt(R);
                this.G = P_Convert.ObjToInt(G);
                this.B = P_Convert.ObjToInt(B);
            }
        }
        public static void Init()
        {
            string TableName = "FontColors";
            string CreateSql = @"
CREATE TABLE [FontColors](
  [FileUniqueKey] INT, 
  [Key] TEXT, 
  [R] INT, 
  [G] INT, 
  [B] INT
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

                string[] ExpectedCols = { "FileUniqueKey", "Key", "R", "G", "B" };
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

        public static FontColor FindColor(int FileUniqueKey, string Key)
        {
            const string SqlOrder =
                "SELECT * FROM FontColors WHERE FileUniqueKey = @fileUniqueKey AND [Key] = @key;";
            List<Dictionary<string, object>> NTable = Phoenix.LocalDB.ExecuteQuery(
                SqlOrder,
                SqliteSql.Parameter("@fileUniqueKey", FileUniqueKey),
                SqliteSql.Parameter("@key", Key));
            if (NTable.Count > 0)
            {
                var Row = NTable[0]; // Dictionary<string, object>

                return new FontColor(
                    Row["FileUniqueKey"],
                    Row["Key"],
                    Row["R"],
                    Row["G"],
                    Row["B"]
                );
            }

            return null;
        }

        public static bool DeleteColor(int FileUniqueKey, string Key)
        {
            const string SqlOrder =
                "DELETE FROM FontColors WHERE FileUniqueKey = @fileUniqueKey AND [Key] = @key;";
            int State = Phoenix.LocalDB.ExecuteNonQuery(
                SqlOrder,
                SqliteSql.Parameter("@fileUniqueKey", FileUniqueKey),
                SqliteSql.Parameter("@key", Key));
            if (State != 0)
            {
                return true;
            }

            return false;
        }

        public static bool SetColor(int FileUniqueKey, string Key,int R,int G,int B)
        {
            if ((R == 255 && G == 255 && B == 255) == false)
            {
                int GetRowID = P_Convert.ObjToInt(Phoenix.LocalDB.ExecuteScalar(
                    "SELECT Rowid FROM FontColors WHERE [FileUniqueKey] = @fileUniqueKey AND [Key] = @key;",
                    SqliteSql.Parameter("@fileUniqueKey", FileUniqueKey),
                    SqliteSql.Parameter("@key", Key)));

                if (GetRowID < 0)
                {
                    const string SqlOrder = @"
INSERT INTO FontColors ([FileUniqueKey], [Key], [R], [G], [B])
VALUES (@fileUniqueKey, @key, @red, @green, @blue);";
                    int State = Phoenix.LocalDB.ExecuteNonQuery(
                        SqlOrder,
                        SqliteSql.Parameter("@fileUniqueKey", FileUniqueKey),
                        SqliteSql.Parameter("@key", Key),
                        SqliteSql.Parameter("@red", R),
                        SqliteSql.Parameter("@green", G),
                        SqliteSql.Parameter("@blue", B));
                    if (State != 0)
                    {
                        return true;
                    }
                }
                else
                {
                    const string SqlOrder = @"
UPDATE FontColors
SET [R] = @red, [G] = @green, [B] = @blue
WHERE Rowid = @rowid;";
                    int State = Phoenix.LocalDB.ExecuteNonQuery(
                        SqlOrder,
                        SqliteSql.Parameter("@red", R),
                        SqliteSql.Parameter("@green", G),
                        SqliteSql.Parameter("@blue", B),
                        SqliteSql.Parameter("@rowid", GetRowID));
                    if (State != 0)
                    {
                        return true;
                    }
                }
            }
            else
            {
                DeleteColor(FileUniqueKey, Key);
            }

            return false;
        }
    }
}
