using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PhoenixEngine.ConvertManager;
using PhoenixEngine.EngineManagement;

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
        public string ModName = "";
        public string FileExtension = "";
        public string UpdateTime = "";
        public string CreateTime = "";

        public UniqueKeyItem() { }

        public UniqueKeyItem(object OriginalKey, object ModName, object FileExtension, object UpdateTime, object CreateTime)
        {
            this.OriginalKey = ConvertHelper.ObjToStr(OriginalKey);
            this.ModName = ConvertHelper.ObjToStr(ModName);
            this.FileExtension = ConvertHelper.ObjToStr(FileExtension);
            this.UpdateTime = ConvertHelper.ObjToStr(UpdateTime);
            this.CreateTime = ConvertHelper.ObjToStr(CreateTime);
        }

        public UniqueKeyItem(int Rowid, string OriginalKey, string ModName, string FileExtension, DateTime UpdateTime, DateTime CreateTime)
        {
            this.Rowid = ConvertHelper.ObjToInt(Rowid);
            this.OriginalKey = ConvertHelper.ObjToStr(OriginalKey);
            this.ModName = ConvertHelper.ObjToStr(ModName);
            this.FileExtension = ConvertHelper.ObjToStr(FileExtension);
            this.UpdateTime = ConvertHelper.DateTimeToStr(UpdateTime);
            this.CreateTime = ConvertHelper.DateTimeToStr(CreateTime);
        }
    }

    public class UniqueKeyGenerator
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
    [ModName] TEXT,
    [FileExtension] TEXT,
    [UpdateTime] TEXT,
    [CreateTime] TEXT
);";
                Engine.LocalDB.ExecuteNonQuery(CreateTableSql);
            }
        }
    }
}
