using System.Data;
using PhoenixEngine.ConvertManager;
using PhoenixEngine.EngineManagement;

namespace PhoenixEngine.TranslateManagement
{
    // Copyright (c) 2025 YD525
    // Licensed under the MIT License.
    // See LICENSE file in the project root for full license information.
    //https://github.com/YD525/PhoenixEngine
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
                this.FileUniqueKey = ConvertHelper.ObjToInt(FileUniqueKey);
                this.Key = ConvertHelper.ObjToStr(Key);
                this.R = ConvertHelper.ObjToInt(R);
                this.G = ConvertHelper.ObjToInt(G);
                this.B = ConvertHelper.ObjToInt(B);
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
            string CheckTableSql = $"SELECT name FROM sqlite_master WHERE type='table' AND name='{TableName}';";
            var Result = Engine.LocalDB.ExecuteScalar(CheckTableSql);

            if (Result != null && Result != DBNull.Value)
            {
                // Table exists, check structure
                List<Dictionary<string, object>> Columns = Engine.LocalDB.ExecuteQuery($"PRAGMA table_info({TableName});");
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
                    Engine.LocalDB.ExecuteNonQuery($"DROP TABLE IF EXISTS [{TableName}];");
                    Engine.LocalDB.ExecuteNonQuery(CreateSql);
                }
            }
            else
            {
                // Create if not exists
                Engine.LocalDB.ExecuteNonQuery(CreateSql);
            }
        }

        public static FontColor? FindColor(int FileUniqueKey, string Key)
        {
            string SqlOrder = "Select * From FontColors Where FileUniqueKey = {0} And Key = '{1}'";
            List<Dictionary<string, object>> NTable = Engine.LocalDB.ExecuteQuery(string.Format(SqlOrder,FileUniqueKey, Key));
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
            string SqlOrder = "Delete From FontColors Where FileUniqueKey = {0} And Key = '{1}'";
            int State = Engine.LocalDB.ExecuteNonQuery(string.Format(SqlOrder, FileUniqueKey, Key));
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
                int GetRowID = ConvertHelper.ObjToInt(Engine.LocalDB.ExecuteScalar(String.Format("Select Rowid From FontColors Where [FileUniqueKey] = {0} And [Key] = '{1}'", FileUniqueKey, Key)));

                if (GetRowID < 0)
                {
                    string SqlOrder = "Insert Into FontColors([FileUniqueKey],[Key],[R],[G],[B])Values({0},'{1}',{2},{3},{4})";
                    int State = Engine.LocalDB.ExecuteNonQuery(string.Format(SqlOrder, FileUniqueKey, Key, R, G, B));
                    if (State != 0)
                    {
                        return true;
                    }
                }
                else
                {
                    string SqlOrder = "UPDate FontColors Set [R] = {1},[G] = {2},[B] = {3} Where Rowid = {0}";
                    int State = Engine.LocalDB.ExecuteNonQuery(string.Format(SqlOrder, GetRowID, R, G, B));
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
