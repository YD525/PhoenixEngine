using System;
using System.Collections.Generic;
using PhoenixEngine.ADO;
using PhoenixEngine.Common;
using PhoenixEngine.Memory;

namespace PhoenixEngine.Engine
{
    public class UniqueKeyItem
    {
        public int Rowid = 0;
        public string OriginalKey = "";
        public string FileName = "";
        public string FileExtension = "";
        public string UpdateTime = "";
        public string CreatTime = "";

        public UniqueKeyItem() { }

        public UniqueKeyItem(object Rowid,object OriginalKey, object FileName, object FileExtension, object UpdateTime, object CreatTime)
        {
            this.Rowid = P_Convert.ObjToInt(Rowid);
            this.OriginalKey = P_Convert.ObjToStr(OriginalKey);
            this.FileName = P_Convert.ObjToStr(FileName);
            this.FileExtension = P_Convert.ObjToStr(FileExtension);
            this.UpdateTime = P_Convert.ObjToStr(UpdateTime);
            this.CreatTime = P_Convert.ObjToStr(CreatTime);
        }

        public UniqueKeyItem(string OriginalKey, string FileName, string FileExtension, DateTime UpdateTime, DateTime CreatTime)
        {
            this.OriginalKey = P_Convert.ObjToStr(OriginalKey);
            this.FileName = P_Convert.ObjToStr(FileName);
            this.FileExtension = P_Convert.ObjToStr(FileExtension);
            this.UpdateTime = P_Convert.DateTimeToStr(UpdateTime);
            this.CreatTime = P_Convert.DateTimeToStr(CreatTime);
        }
    }

    public class UniqueKeyHelper
    {
        public static void Init()
        {
            const string CheckTableSql =
                "SELECT name FROM sqlite_master WHERE type = 'table' AND name = @tableName;";
            var Result = Phoenix.LocalDB.ExecuteScalar(
                CheckTableSql,
                SqliteSql.Parameter("@tableName", "UniqueKeys"));

            if (Result == null || Result == DBNull.Value)
            {
                string CreateTableSql = @"
CREATE TABLE [UniqueKeys](
    [OriginalKey] TEXT,
    [FileName] TEXT,
    [FileExtension] TEXT,
    [UpdateTime] TEXT,
    [CreatTime] TEXT
);";
                Phoenix.LocalDB.ExecuteNonQuery(CreateTableSql);
            }
        }

        public static string RowidToOriginalKey(int RowID)
        {
            const string SqlOrder = "SELECT OriginalKey FROM UniqueKeys WHERE Rowid = @rowid;";
            string GetOriginalKey = SQLSafeCodec.Decode(P_Convert.ObjToStr(Phoenix.LocalDB.ExecuteScalar(
                SqlOrder,
                SqliteSql.Parameter("@rowid", RowID))));
            return GetOriginalKey;
        }

        /// <summary>
        /// Get the file extension from a file path. Returns empty string if none.
        /// </summary>
        /// <param name="FilePath">Full file path</param>
        /// <returns>File extension including the dot (e.g., ".txt") or empty string</returns>
        private static string GetFileExtension(string FilePath)
        {
            if (string.IsNullOrEmpty(FilePath)) return string.Empty;
            try
            {
                string Extension = System.IO.Path.GetExtension(FilePath);
                return Extension ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Get the file name (including extension) from a full file path.
        /// </summary>
        /// <param name="FilePath">Full file path</param>
        /// <returns>File name with extension</returns>
        private static string GetFileName(string FilePath)
        {
            if (string.IsNullOrEmpty(FilePath)) return string.Empty;

            try
            {
                var FileInfo = new System.IO.FileInfo(FilePath);
                return FileInfo.Name;
            }
            catch
            {
                return string.Empty;
            }
        }

      
        /// <summary>
        /// Get the total number of records in the UniqueKeys table.
        /// </summary>
        /// <returns>Count of UniqueKeys records</returns>
        public static int GetUniqueKeysCount()
        {
            string SqlOrder = "SELECT COUNT(*) FROM UniqueKeys;";
            int Count = P_Convert.ObjToInt(Phoenix.LocalDB.ExecuteScalar(SqlOrder));
            return Count;
        }

        /// <summary>
        /// Add a file to the UniqueKeys table and return its Rowid.
        /// If the file already exists (by exact key or fuzzy matching), updates the existing record.
        /// </summary>
        /// <param name="FilePath">Full path to the file</param>
        /// <param name="CanSkipFuzzyMatching">Whether to skip fuzzy matching</param>
        /// <returns>Rowid of the added or matched record. -1 if nothing added.</returns>
        public static int AddItemByReturn(ref UniqueKeyItem GenUniqueKeyItem, string FilePath,bool CanSkipFuzzyMatching = false)
        {
            string SourceOriginalKey = GetFileName(FilePath);

            GenUniqueKeyItem = new UniqueKeyItem(
               SourceOriginalKey,
               GetFileName(FilePath),
               GetFileExtension(FilePath),
               DateTime.Now,
               DateTime.Now);

            int UpdateRowid;
            if (!UpdateItem(GenUniqueKeyItem, FilePath, out UpdateRowid))
            {
                string SqlOrder = "";

                ////Scan history files Fuzzy matching Key

                //if (!CanSkipFuzzyMatching)
                //{
                //    SqlOrder = "Select Rowid,OriginalKey From UniqueKeys Where 1 = 1;";
                //    DataTable NTable = Engine.LocalDB.ExecuteDataTable(
                //        SqlOrder
                //    );

                //    for (int i = 0; i < NTable.Rows.Count; i++)
                //    {
                //        string OriginalKey = ConvertHelper.ObjToStr(NTable.Rows[i]["OriginalKey"]);
                //        if (BlockHashComparer.MatchFile(OriginalKey, SourceOriginalKey))
                //        {
                //            int Rowid = ConvertHelper.ObjToInt(NTable.Rows[i]["Rowid"]);

                //            UpdateOldFiles(OriginalKey, GenUniqueKeyItem);

                //            return Rowid;
                //        }
                //    }
                //}

                //This is the new file

                SqlOrder = @"
INSERT INTO UniqueKeys (OriginalKey, FileName, FileExtension, UpdateTime, CreatTime)
VALUES (@originalKey, @fileName, @fileExtension, @updateTime, @createTime);";

                int State = P_Convert.ObjToInt(Phoenix.LocalDB.ExecuteNonQuery(
                    SqlOrder,
                    SqliteSql.Parameter("@originalKey", SQLSafeCodec.Encode(GenUniqueKeyItem.OriginalKey)),
                    SqliteSql.Parameter("@fileName", SQLSafeCodec.Encode(GenUniqueKeyItem.FileName)),
                    SqliteSql.Parameter("@fileExtension", GenUniqueKeyItem.FileExtension),
                    SqliteSql.Parameter("@updateTime", GenUniqueKeyItem.UpdateTime),
                    SqliteSql.Parameter("@createTime", GenUniqueKeyItem.CreatTime)));

                if (State != 0)
                {
                    int NewRowid = P_Convert.ObjToInt(
                    Phoenix.LocalDB.ExecuteScalar(
                        "SELECT Rowid FROM UniqueKeys WHERE OriginalKey = @originalKey;",
                        SqliteSql.Parameter(
                            "@originalKey",
                            SQLSafeCodec.Encode(GenUniqueKeyItem.OriginalKey))));
                    return NewRowid;
                }
            }
            else
            {
                return UpdateRowid;
            }

            return -1;
        }

        /// <summary>
        /// Update an existing UniqueKeys record with a new file info, matched by OriginalKey.
        /// </summary>
        /// <param name="OriginalKey">OriginalKey of the record to update</param>
        /// <param name="KeyItem">New file info</param>
        /// <returns>True if update affected rows, false otherwise</returns>
        public static bool UpdateOldFiles(string OriginalKey, UniqueKeyItem KeyItem)
        {
            const string SqlOrder = @"
UPDATE UniqueKeys
SET FileName = @fileName, FileExtension = @fileExtension, UpdateTime = @updateTime
WHERE OriginalKey = @originalKey;";
            int State = Phoenix.LocalDB.ExecuteNonQuery(
                SqlOrder,
                SqliteSql.Parameter("@fileName", SQLSafeCodec.Encode(KeyItem.FileName)),
                SqliteSql.Parameter("@fileExtension", KeyItem.FileExtension),
                SqliteSql.Parameter("@updateTime", KeyItem.UpdateTime),
                SqliteSql.Parameter("@originalKey", SQLSafeCodec.Encode(OriginalKey)));
            if (State != 0)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Check if a record with the given OriginalKey exists and update it.
        /// </summary>
        /// <param name="GenUniqueKeyItem">UniqueKeyItem to update</param>
        /// <param name="FilePath">File path (not used here)</param>
        /// <param name="Rowid">Output Rowid of existing record, 0 if not exists</param>
        /// <returns>True if record existed and updated, false if new</returns>
        private static bool UpdateItem(UniqueKeyItem GenUniqueKeyItem, string FilePath, out int Rowid)
        {
            Rowid = 0;

            string SqlOrder = "SELECT Rowid FROM UniqueKeys WHERE [OriginalKey] = @originalKey;";

            int GetRowid = P_Convert.ObjToInt(Phoenix.LocalDB.ExecuteScalar(
                SqlOrder,
                SqliteSql.Parameter("@originalKey", SQLSafeCodec.Encode(GenUniqueKeyItem.OriginalKey))));

            if (GetRowid > 0)
            {
                Rowid = GetRowid;

                SqlOrder = @"
UPDATE UniqueKeys
SET FileName = @fileName,
    FileExtension = @fileExtension,
    UpdateTime = @updateTime,
    CreatTime = @createTime
WHERE [OriginalKey] = @originalKey;";

                int State = Phoenix.LocalDB.ExecuteNonQuery(
                    SqlOrder,
                    SqliteSql.Parameter("@fileName", SQLSafeCodec.Encode(GenUniqueKeyItem.FileName)),
                    SqliteSql.Parameter("@fileExtension", GenUniqueKeyItem.FileExtension),
                    SqliteSql.Parameter("@updateTime", GenUniqueKeyItem.UpdateTime),
                    SqliteSql.Parameter("@createTime", GenUniqueKeyItem.CreatTime),
                    SqliteSql.Parameter("@originalKey", SQLSafeCodec.Encode(GenUniqueKeyItem.OriginalKey)));

                return true;
            }

            return false;
        }

        /// <summary>
        /// Query a UniqueKeyItem by its Rowid (primary key).
        /// </summary>
        /// <param name="Rowid">The Rowid of the record in the UniqueKeys table (primary key).</param>
        /// <returns>The matching UniqueKeyItem if found; otherwise, null.</returns>
        public UniqueKeyItem QueryUniqueKey(int Rowid)
        {
            const string SqlOrder = "SELECT Rowid, * FROM UniqueKeys WHERE Rowid = @rowid;";
            List<Dictionary<string, object>> NTable = Phoenix.LocalDB.ExecuteQuery(
                SqlOrder,
                SqliteSql.Parameter("@rowid", Rowid));

            if (NTable.Count > 0)
            {
                for (int i = 0; i < NTable.Count; i++)
                {
                    var Row = NTable[i];

                    return new UniqueKeyItem(
                        Row["Rowid"],
                        SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["OriginalKey"])),
                        SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["FileName"])),
                        Row["FileExtension"],
                        Row["UpdateTime"],
                        Row["CreatTime"]
                    );
                }
            }

            return null;
        }

        /// <summary>
        /// Query the 10 most recent UniqueKeyItem records from the UniqueKeys table.
        /// </summary>
        /// <remarks>
        /// Records are sorted by Rowid in descending order, so the latest entries appear first.
        /// </remarks>
        /// <returns>List of up to 10 UniqueKeyItem objects representing the newest records.</returns>
        public List<UniqueKeyItem> QueryHotUniqueKeys(int Limit = 10)
        {
            List<UniqueKeyItem> UniqueKeyItems = new List<UniqueKeyItem>();

            const string SqlOrder =
                "SELECT Rowid, * FROM UniqueKeys ORDER BY Rowid DESC LIMIT @limit;";

            List<Dictionary<string, object>> NTable = Phoenix.LocalDB.ExecuteQuery(
                SqlOrder,
                SqliteSql.Parameter("@limit", Limit));

            if (NTable.Count > 0)
            {
                for (int i = 0; i < NTable.Count; i++)
                {
                    var Row = NTable[i]; // Dictionary<string, object>

                    UniqueKeyItems.Add(new UniqueKeyItem(
                        Row["Rowid"],
                        SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["OriginalKey"])),
                        SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["FileName"])),
                        Row["FileExtension"],
                        Row["UpdateTime"],
                        Row["CreatTime"]
                    ));
                }
            }

            return UniqueKeyItems;
        }

        /// <summary>
        /// Query all UniqueKeyItem records from the UniqueKeys table.
        /// </summary>
        /// <returns>List of all UniqueKeyItem objects in the table.</returns>
        public List<UniqueKeyItem> QueryUniqueKeys()
        {
            List<UniqueKeyItem> UniqueKeyItems = new List<UniqueKeyItem>();

            string SqlOrder = "Select Rowid,* From UniqueKeys Where 1 = 1";
            List<Dictionary<string, object>> NTable = Phoenix.LocalDB.ExecuteQuery(SqlOrder);

            if (NTable.Count > 0)
            {
                for (int i = 0; i < NTable.Count; i++)
                {
                    var Row = NTable[i]; // Dictionary<string, object>

                    UniqueKeyItems.Add(new UniqueKeyItem(
                        Row["Rowid"],
                        SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["OriginalKey"])),
                        SQLSafeCodec.Decode(P_Convert.ObjToStr(Row["FileName"])),
                        Row["FileExtension"],
                        Row["UpdateTime"],
                        Row["CreatTime"]
                    ));
                }
            }

            return UniqueKeyItems;
        }

        /// <summary>
        /// Delete a UniqueKeyItem record from the UniqueKeys table by its Rowid (primary key).
        /// </summary>
        /// <param name="Rowid">The Rowid of the record to delete.</param>
        /// <returns>True if a record was deleted; otherwise, false.</returns>
        public bool DeleteUniqueKeyByRowid(int Rowid)
        {
            const string SqlOrder = "DELETE FROM UniqueKeys WHERE Rowid = @rowid;";
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

    public static class AITranslationMemoryR
    {
        public static int Optimization(this AITranslationMemory A, params object[] Any){ return 1; }
    }
}
