using System;
using System.Collections.Generic;
using System.Data.SQLite;
using PhoenixEngine.ConvertManager;
using PhoenixEngine.DataBaseManagement;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.LanguageDetector;

namespace PhoenixEngine.LanguageManagement
{
    public class ChineseVariantMap
    {
        public static void Init()
        {
            string CheckTableSql = "SELECT name FROM sqlite_master WHERE type='table' AND name='ChineseVariantMap';";
            var Result = Phoenix.LocalDB.ExecuteScalar(CheckTableSql);

            if (Result == null || Result == DBNull.Value)
            {
                CreateNewTable();
            }

            ReadRamChars();
        }

        private static List<string> RamWords = new List<string>();

        private static void CreateNewTable()
        {
            string SqlOrder = @"
            CREATE TABLE [ChineseVariantMap](
            [Simplified] TEXT, 
            [Traditional] TEXT, 
            [MatchType] INT
            );";

            Phoenix.LocalDB.ExecuteNonQuery(SqlOrder);
        }

        private static void ReadRamChars()
        {
            RamWords.Clear();

            string SqlOrder = "Select Traditional From ChineseVariantMap Where MatchType = 1;";
     
            List<Dictionary<string, object>> GetResult = Phoenix.LocalDB.ExecuteQuery(SqlOrder);

            for (int i = 0; i < GetResult.Count; i++)
            {
                var Row = GetResult[i];
                string GetStr = ConvertHelper.ObjToStr(Row["Traditional"]);
                if (!RamWords.Contains(GetStr))
                {
                    RamWords.Add(GetStr);
                }
            }

            //Thanks to 撒倫 for providing the comparison phrases. 
            //https://forum.gamer.com.tw/C.php?bsn=2526&snA=46062

            RamWords.Add("麵");
            RamWords.Add("隻");
            RamWords.Add("彆");
            RamWords.Add("穀");
            RamWords.Add("製");
            RamWords.Add("係");
            RamWords.Add("鬥");
            RamWords.Add("誌");
            RamWords.Add("妳");
        }


        public ZHType CheckLangType(string Line)
        {
            Line = SqlSafeCodec.Encode(Line);

            ZHType SetType = ZHType.Null;

            if (SimplifiedChineseHelper.ContainsSimplifiedChinese(Line))
            {
                foreach (var GetWord in new List<string>(ChineseVariantMap.RamWords))
                {
                    if (Line.Contains(GetWord))
                    {
                        return ZHType.Traditional;
                    }
                }

                SetType = ZHType.Simplified;

                string SqlOrder = @"SELECT 1 FROM ChineseVariantMap WHERE MatchType = 0 AND instr('{0}', Traditional) > 0 LIMIT 1;";

                var Result = Phoenix.LocalDB.ExecuteScalar(string.Format(SqlOrder, Line));

                if (Result != null)
                {
                    return ZHType.Traditional;
                }
            }
         
            return SetType;
        }


        public string SimplifiedToTraditional(string Line)
        {
            return "";
        }

        public string TraditionalToSimplified(string Line)
        {
            return "";
        }

    }

    public enum ZHType
    {
       Null = 2, Traditional = 0, Simplified = 1
    }
}
