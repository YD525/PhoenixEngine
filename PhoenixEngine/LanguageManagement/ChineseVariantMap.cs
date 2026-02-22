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
            RamWords.Add("麵");
            RamWords.Add("隻");
            RamWords.Add("彆");
            RamWords.Add("穀");
            RamWords.Add("製");
            RamWords.Add("係");
            RamWords.Add("鬥");
            RamWords.Add("誌");
            RamWords.Add("妳");

            //https://zhconvert.org/
            //YD525 Small Patch~
            RamWords.Add("學");RamWords.Add("燒");RamWords.Add("賣");
            RamWords.Add("愛");RamWords.Add("歡");RamWords.Add("車");
            RamWords.Add("體");RamWords.Add("將");RamWords.Add("戰");
            RamWords.Add("裡");RamWords.Add("館");RamWords.Add("醫");
            RamWords.Add("藥");RamWords.Add("魚");RamWords.Add("點");
            RamWords.Add("線");RamWords.Add("還");RamWords.Add("辦");
            RamWords.Add("關");RamWords.Add("問");RamWords.Add("萬");
            RamWords.Add("樓");RamWords.Add("頭");RamWords.Add("燈");
            RamWords.Add("葉");RamWords.Add("師");RamWords.Add("門");
            RamWords.Add("鐘");RamWords.Add("號");RamWords.Add("場");
            RamWords.Add("條");RamWords.Add("樹");RamWords.Add("純");
            RamWords.Add("廟");RamWords.Add("院");RamWords.Add("臺");
            RamWords.Add("陽");RamWords.Add("島");RamWords.Add("網");
            RamWords.Add("視");RamWords.Add("劇");RamWords.Add("灣");
            RamWords.Add("詞");RamWords.Add("樂");RamWords.Add("攝");
            RamWords.Add("畫");RamWords.Add("隊");RamWords.Add("勝");
            RamWords.Add("軍");RamWords.Add("數");RamWords.Add("歲");
            RamWords.Add("槍");RamWords.Add("劍");RamWords.Add("繩");
            RamWords.Add("國");RamWords.Add("際");RamWords.Add("爭");
            RamWords.Add("馬");RamWords.Add("壘");RamWords.Add("蟲");
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
