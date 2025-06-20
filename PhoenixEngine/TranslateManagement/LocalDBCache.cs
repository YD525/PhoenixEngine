
using PhoenixEngine.ConvertManager;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.TranslateCore;

namespace PhoenixEngine.TranslateManagement
{
    // Copyright (C) 2025 YD525
    // Licensed under the GNU GPLv3
    // See LICENSE for details
    //https://github.com/YD525/PhoenixEngine
    public class LocalTransItem
    {
        public string ModName = "";
        public string Key = "";
        public int To = 0;
        public string Result = "";
        public int Index = 0;
        public int ColorSet = 0;

        public LocalTransItem(string ModName,string Key,string Result)
        {
            this.ModName = ModName;
            this.Key = Key;
            this.To = (int)EngineConfig.TargetLanguage;
            this.Result = Result;
            this.Index = 0;
            this.ColorSet = ConvertHelper.ObjToInt(ColorSet);
        }

        public LocalTransItem(object ModName, object Key, object To, object Result)
        {
            this.ModName = ConvertHelper.ObjToStr(ModName);
            this.Key = ConvertHelper.ObjToStr(Key);
            this.To = ConvertHelper.ObjToInt(To);
            this.Result = ConvertHelper.ObjToStr(Result);
            this.Index = 0; 
            this.ColorSet = 0;
        }
    }
    public class LocalDBCache
    {
        public static void Init()
        {
            string CheckTableSql = "SELECT name FROM sqlite_master WHERE type='table' AND name='LocalTranslation';";
            var Result = Engine.LocalDB.ExecuteScalar(CheckTableSql);

            if (Result == null || Result == DBNull.Value)
            {
                string CreateTableSql = @"
CREATE TABLE [LocalTranslation](
  [ModName] TEXT, 
  [Key] TEXT, 
  [To] INT, 
  [Result] TEXT, 
  [Index] INT, 
  [ColorSet] INT
);";
                Engine.LocalDB.ExecuteNonQuery(CreateTableSql);
            }
        }
        public static bool DeleteCacheByModName(string ModName)
        {
            try
            {
                string SqlOrder = "Delete From LocalTranslation Where [ModName] = '{0}' And [To] = {1}";

                int State = Engine.LocalDB.ExecuteNonQuery(string.Format(SqlOrder, ModName, (int)EngineConfig.TargetLanguage));

                if (State != 0)
                {
                    return true;
                }

                return false;
            }
            catch { return false; }
        }

        public static bool DeleteCacheByResult(string ModName,string ResultText)
        {
            try
            {
                string SqlOrder = "Delete From LocalTranslation Where [ModName] = '{0}' And [Result] = '{1}' And [To] = {2}";

                int State = Engine.LocalDB.ExecuteNonQuery(string.Format(SqlOrder, ModName, System.Web.HttpUtility.HtmlEncode(ResultText),(int)EngineConfig.TargetLanguage));

                if (State != 0)
                {
                    return true;
                }

                return false;
            }
            catch { return false; }
        }

        public static bool DeleteCache(string ModName,string Key)
        {
            try
            {
                string SqlOrder = "Delete From LocalTranslation Where [ModName] = '{0}' And [Key] = '{1}' And [To] = {2}";

                int State = Engine.LocalDB.ExecuteNonQuery(string.Format(SqlOrder, ModName, Key,(int)EngineConfig.TargetLanguage));

                if (State!=0)
                {
                    return true;
                }

                return false;
            }
            catch { return false; }
        }

        public static string GetCacheText(string ModName,string Key)
        {
            try
            {
                string SqlOrder = "Select Result From LocalTranslation Where [ModName] = '{0}' And[Key] = '{1}' And [To] = {2}";

                string GetText = ConvertHelper.ObjToStr(Engine.LocalDB.ExecuteScalar(string.Format(SqlOrder, ModName, Key,(int)EngineConfig.TargetLanguage)));

                if (GetText.Trim().Length > 0)
                {
                    return System.Web.HttpUtility.HtmlDecode(GetText);
                }

                return string.Empty;
            }
            catch { return string.Empty; }
        }

        public static string FindCache(string ModName,string Key)
        {
            return FindCache(ModName, Key, (int)EngineConfig.TargetLanguage);
        }


        public static string FindCache(string ModName,string Key,int To)
        {
            try
            {
                string SqlOrder = "Select Result From LocalTranslation Where [ModName] = '{0}' And [Key] = '{1}' And [To] = {2}";

                string GetResult = ConvertHelper.ObjToStr(Engine.LocalDB.ExecuteScalar(string.Format(SqlOrder,ModName,Key,To)));

                if (GetResult.Trim().Length > 0)
                {
                    return System.Web.HttpUtility.HtmlDecode(GetResult);
                }

                return string.Empty;
            }
            catch { return string.Empty; }
        }    

        public static bool UPDateLocalTransItem(LocalTransItem Item)
        {
            if (Item.ModName.Trim().Length == 0)
            {
                return false;
            }

            string FindCache = CloudDBCache.FindCache(Item.ModName, Item.Key);

            if (FindCache.Trim().Length > 0)
            {
                if (FindCache.Equals(Item.Result))
                {
                    DeleteCache(Item.ModName, Item.Key);
                    return false;
                }
            }

            int GetRowID = ConvertHelper.ObjToInt(Engine.LocalDB.ExecuteScalar(String.Format("Select Rowid From LocalTranslation Where [ModName] = '{0}' And [Key] = '{1}' And [To] = {2}", Item.ModName,Item.Key,Item.To)));

            if (GetRowID < 0 && Item.Result.Trim().Length > 0)
            {
                string SqlOrder = "Insert Into LocalTranslation([ModName],[Key],[To],[Result],[Index],[ColorSet])Values('{0}','{1}',{2},'{3}',{4},{5})";
                int State = Engine.LocalDB.ExecuteNonQuery(string.Format(SqlOrder,
                    Item.ModName,
                    Item.Key,
                    Item.To,
                    System.Web.HttpUtility.HtmlEncode(Item.Result),
                    Item.Index,
                    Item.ColorSet
                    ));
                if (State != 0)
                {
                    return true;
                }
                return false;
            }
            else
            {
                string SqlOrder = "UPDate LocalTranslation Set [Result] = '{1}',[Index] = {2},[ColorSet] = {3} Where Rowid = {0}";
                int State = Engine.LocalDB.ExecuteNonQuery(string.Format(SqlOrder,GetRowID,System.Web.HttpUtility.HtmlEncode(Item.Result),Item.Index,Item.ColorSet));
                if (State != 0)
                {
                    return true;
                }
                return false;
            }  
        }

    }
}
