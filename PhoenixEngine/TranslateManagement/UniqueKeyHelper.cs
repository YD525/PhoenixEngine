using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using PhoenixEngine.ConvertManager;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.FileManagement;

namespace PhoenixEngine.TranslateManagement
{
    // Copyright (c) 2025 YD525
    // Licensed under the MIT License.
    // See LICENSE file in the project root for full license information.
    //https://github.com/YD525/PhoenixEngine
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
            this.Rowid = ConvertHelper.ObjToInt(Rowid);
            this.OriginalKey = ConvertHelper.ObjToStr(OriginalKey);
            this.FileName = ConvertHelper.ObjToStr(FileName);
            this.FileExtension = ConvertHelper.ObjToStr(FileExtension);
            this.UpdateTime = ConvertHelper.ObjToStr(UpdateTime);
            this.CreatTime = ConvertHelper.ObjToStr(CreatTime);
        }

        public UniqueKeyItem(string OriginalKey, string FileName, string FileExtension, DateTime UpdateTime, DateTime CreatTime)
        {
            this.OriginalKey = ConvertHelper.ObjToStr(OriginalKey);
            this.FileName = ConvertHelper.ObjToStr(FileName);
            this.FileExtension = ConvertHelper.ObjToStr(FileExtension);
            this.UpdateTime = ConvertHelper.DateTimeToStr(UpdateTime);
            this.CreatTime = ConvertHelper.DateTimeToStr(CreatTime);
        }
    }

    public class UniqueKeyHelper
    {
        public static void Init()
        {
            string CheckTableSql = "SELECT name FROM sqlite_master WHERE type='table' AND name='UniqueKeys';";
            var Result = Engine.LocalDB.ExecuteScalar(CheckTableSql);

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
                Engine.LocalDB.ExecuteNonQuery(CreateTableSql);
            }
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
        /// Generate a block-based MD5 key string for a file.
        /// If failed, returns the file name.
        /// </summary>
        /// <param name="filePath">Full path to the file</param>
        /// <returns>Joined MD5 hash string or file name if failed</returns>
        public static string GenOriginalKeyByFile(string FilePath)
        {
            string Key = "";

            try
            {
                Key = BlockHashComparer.JoinHashes(
                BlockHashComparer.GetBlockCRC32(FilePath)
                );
            }
            catch
            {
                return GetFileName(FilePath);
            }

            return Key;
        }

        /// <summary>
        /// Get the total number of records in the UniqueKeys table.
        /// </summary>
        /// <returns>Count of UniqueKeys records</returns>
        public static int GetUniqueKeysCount()
        {
            string SqlOrder = "SELECT COUNT(*) FROM UniqueKeys;";
            int Count = ConvertHelper.ObjToInt(Engine.LocalDB.ExecuteScalar(SqlOrder));
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

            int UPDateRowid;
            if (!UPDateItem(GenUniqueKeyItem, FilePath, out UPDateRowid))
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

                SqlOrder = "Insert Into UniqueKeys(OriginalKey,FileName,FileExtension,UpdateTime,CreatTime)Values('{0}','{1}','{2}','{3}','{4}')";

                int State = ConvertHelper.ObjToInt(Engine.LocalDB.ExecuteNonQuery(string.Format(SqlOrder,
                    GenUniqueKeyItem.OriginalKey,
                    GenUniqueKeyItem.FileName,
                    GenUniqueKeyItem.FileExtension,
                    GenUniqueKeyItem.UpdateTime,
                    GenUniqueKeyItem.CreatTime
                    )));

                if (State != 0)
                {
                    int NewRowid = ConvertHelper.ObjToInt(
                    Engine.LocalDB.ExecuteScalar(
                     $"Select Rowid From UniqueKeys Where OriginalKey = '{GenUniqueKeyItem.OriginalKey}';"
                    ));
                    return NewRowid;
                }
            }
            else
            {
                return UPDateRowid;
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
            string SqlOrder = "UPDate UniqueKeys Set FileName = '{1}',FileExtension = '{2}',UpdateTime = '{3}' Where OriginalKey = '{0}';";
            int State = Engine.LocalDB.ExecuteNonQuery(string.Format(SqlOrder,OriginalKey,KeyItem.FileName,KeyItem.FileExtension,KeyItem.UpdateTime));
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
        private static bool UPDateItem(UniqueKeyItem GenUniqueKeyItem, string FilePath, out int Rowid)
        {
            Rowid = 0;

            string SqlOrder = "Select Rowid From UniqueKeys Where [OriginalKey] = '{0}';";

            int GetRowid = ConvertHelper.ObjToInt(Engine.LocalDB.ExecuteScalar(string.Format(SqlOrder, GenUniqueKeyItem.OriginalKey)));

            if (GetRowid > 0)
            {
                Rowid = GetRowid;

                SqlOrder = "UPDate UniqueKeys Set FileName = '{1}',FileExtension = '{2}',UpdateTime = '{3}',CreatTime = '{4}' Where [OriginalKey] = '{0}';";

                int State = Engine.LocalDB.ExecuteNonQuery(
                    string.Format(SqlOrder,
                    GenUniqueKeyItem.OriginalKey,
                    GenUniqueKeyItem.FileName,
                    GenUniqueKeyItem.FileExtension,
                    GenUniqueKeyItem.UpdateTime,
                    GenUniqueKeyItem.CreatTime
                    ));

                return true;
            }

            return false;
        }

        /// <summary>
        /// Query a UniqueKeyItem by its Rowid (primary key).
        /// </summary>
        /// <param name="Rowid">The Rowid of the record in the UniqueKeys table (primary key).</param>
        /// <returns>The matching UniqueKeyItem if found; otherwise, null.</returns>
        public UniqueKeyItem? QueryUniqueKey(int Rowid)
        {
            string SqlOrder = "Select Rowid,* From UniqueKeys Where Rowid = {0}";
            DataTable NTable = Engine.LocalDB.ExecuteDataTable(string.Format(SqlOrder,Rowid));
            
            if (NTable.Rows.Count > 0)
            {
                for (int i = 0; i < NTable.Rows.Count; i++)
                {
                    return new UniqueKeyItem(
                        NTable.Rows[i]["Rowid"],
                        NTable.Rows[i]["OriginalKey"],
                        NTable.Rows[i]["FileName"],
                        NTable.Rows[i]["FileExtension"],
                        NTable.Rows[i]["UpdateTime"],
                        NTable.Rows[i]["CreatTime"]
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

            string SqlOrder = "SELECT Rowid, * FROM UniqueKeys ORDER BY Rowid DESC LIMIT " + Limit.ToString() + ";";

            DataTable NTable = Engine.LocalDB.ExecuteDataTable(SqlOrder);

            if (NTable.Rows.Count > 0)
            {
                for (int i = 0; i < NTable.Rows.Count; i++)
                {
                    UniqueKeyItems.Add(new UniqueKeyItem(
                        NTable.Rows[i]["Rowid"],
                        NTable.Rows[i]["OriginalKey"],
                        NTable.Rows[i]["FileName"],
                        NTable.Rows[i]["FileExtension"],
                        NTable.Rows[i]["UpdateTime"],
                        NTable.Rows[i]["CreatTime"]
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
            DataTable NTable = Engine.LocalDB.ExecuteDataTable(SqlOrder);

            if (NTable.Rows.Count > 0)
            {
                for (int i = 0; i < NTable.Rows.Count; i++)
                {
                    UniqueKeyItems.Add(new UniqueKeyItem(
                        NTable.Rows[i]["Rowid"],
                        NTable.Rows[i]["OriginalKey"],
                        NTable.Rows[i]["FileName"],
                        NTable.Rows[i]["FileExtension"],
                        NTable.Rows[i]["UpdateTime"],
                        NTable.Rows[i]["CreatTime"]
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
            string SqlOrder = "Delete From UniqueKeys Where Rowid = {0}";
            int State = Engine.LocalDB.ExecuteNonQuery(string.Format(SqlOrder,Rowid));
            if (State != 0)
            {
                return true;
            }
            return false;
        }
    }
}
