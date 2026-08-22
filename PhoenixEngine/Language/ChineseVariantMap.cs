using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using PhoenixEngine.ADO;
using PhoenixEngine.Common;
using PhoenixEngine.Request;

namespace PhoenixEngine.Language
{
    public class ChineseVariantMap
    {
        private static readonly HttpHelper HttpTransport = new HttpHelper();

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
                string GetStr = P_Convert.ObjToStr(Row["Traditional"]);
                if (!RamWords.Contains(GetStr))
                {
                    RamWords.Add(GetStr);
                }
            }

            //Thanks to 撒倫 for providing the comparison phrases. 
            RamWords.Add("麵"); RamWords.Add("隻"); RamWords.Add("彆");
            RamWords.Add("穀"); RamWords.Add("製"); RamWords.Add("係");
            RamWords.Add("鬥"); RamWords.Add("誌"); RamWords.Add("妳");

            //CHIOUSF 
            RamWords.Add("牠"); RamWords.Add("祂"); RamWords.Add("鉈");

            //https://zhconvert.org/
            //YD525 Small Patch~
            RamWords.Add("學"); RamWords.Add("燒"); RamWords.Add("賣");
            RamWords.Add("愛"); RamWords.Add("歡"); RamWords.Add("車");
            RamWords.Add("體"); RamWords.Add("將"); RamWords.Add("戰");
            RamWords.Add("裡"); RamWords.Add("館"); RamWords.Add("醫");
            RamWords.Add("藥"); RamWords.Add("魚"); RamWords.Add("點");
            RamWords.Add("線"); RamWords.Add("還"); RamWords.Add("辦");
            RamWords.Add("關"); RamWords.Add("問"); RamWords.Add("萬");
            RamWords.Add("樓"); RamWords.Add("頭"); RamWords.Add("燈");
            RamWords.Add("葉"); RamWords.Add("師"); RamWords.Add("門");
            RamWords.Add("鐘"); RamWords.Add("號"); RamWords.Add("場");
            RamWords.Add("條"); RamWords.Add("樹"); RamWords.Add("島");
            RamWords.Add("廟"); RamWords.Add("臺"); RamWords.Add("顯");
            RamWords.Add("網"); RamWords.Add("啟"); RamWords.Add("較");
            RamWords.Add("視"); RamWords.Add("劇"); RamWords.Add("灣");
            RamWords.Add("詞"); RamWords.Add("樂"); RamWords.Add("攝");
            RamWords.Add("畫"); RamWords.Add("隊"); RamWords.Add("勝");
            RamWords.Add("軍"); RamWords.Add("數"); RamWords.Add("歲");
            RamWords.Add("槍"); RamWords.Add("劍"); RamWords.Add("繩");
            RamWords.Add("國"); RamWords.Add("際"); RamWords.Add("葉");
            RamWords.Add("爭"); RamWords.Add("語"); RamWords.Add("凍");
            RamWords.Add("寧"); RamWords.Add("華"); RamWords.Add("樂");
            RamWords.Add("壘"); RamWords.Add("傾"); RamWords.Add("會");
            RamWords.Add("蟲"); RamWords.Add("純"); RamWords.Add("變");
            RamWords.Add("風"); RamWords.Add("斬"); RamWords.Add("異");
            RamWords.Add("陽"); RamWords.Add("僅"); RamWords.Add("馬");
            RamWords.Add("塵"); RamWords.Add("極"); RamWords.Add("術");
            RamWords.Add("麗"); RamWords.Add("幹"); RamWords.Add("複");
            RamWords.Add("團"); RamWords.Add("陰"); RamWords.Add("後");
            RamWords.Add("強"); RamWords.Add("辭"); RamWords.Add("綠");
            RamWords.Add("麼"); RamWords.Add("轉"); RamWords.Add("書");
            RamWords.Add("現"); RamWords.Add("樸"); RamWords.Add("裹");
            RamWords.Add("對"); RamWords.Add("錯"); RamWords.Add("記");
            RamWords.Add("憶"); RamWords.Add("謝"); RamWords.Add("佢");
            RamWords.Add("導"); RamWords.Add("鳳"); RamWords.Add("龍");
            RamWords.Add("裝"); RamWords.Add("備"); RamWords.Add("禮");
            RamWords.Add("黏"); RamWords.Add("聯"); RamWords.Add("諾");
            RamWords.Add("約"); RamWords.Add("縛"); RamWords.Add("緊");
            RamWords.Add("緊"); RamWords.Add("貞"); RamWords.Add("訂");
            RamWords.Add("閱"); RamWords.Add("讀"); RamWords.Add("為");
        }


        public static ZHType CheckLangType(string Line)
        {
            Line = SQLSafeCodec.Encode(Line);

            ZHType SetType = ZHType.Null;

            if (ZHHelper.ContainsZH(Line))
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

        public static string SimplifiedToTraditionalByReq(string Str)
        {
            ZHConvertJson ConvertJson = new ZHConvertJson();
            ConvertJson.apiKey = "";
            ConvertJson.cleanUpText = 0;
            ConvertJson.converter = "Taiwan";
            ConvertJson.diffCharLevel = 0;
            ConvertJson.diffContextLines = 1;
            ConvertJson.diffEnable = 1;
            ConvertJson.diffIgnoreCase = 0;
            ConvertJson.diffIgnoreWhiteSpaces = 0;
            ConvertJson.diffTemplate = "Inline";
            ConvertJson.ensureNewlineAtEof = 0;
            ConvertJson.ignoreTextStyles = "";
            ConvertJson.jpStyleConversionStrategy = "protectOnlySameOrigin";
            ConvertJson.jpTextConversionStrategy = "protectOnlySameOrigin";
            ConvertJson.jpTextStyles = "";
            ConvertJson.modules = "{\"ChineseVariant\":\"0\",\"Computer\":\"0\",\"EllipsisMark\":\"0\",\"EngNumFWToHW\":\"0\",\"GanToZuo\":\"-1\",\"Gundam\":\"0\",\"HunterXHunter\":\"0\",\"InternetSlang\":\"-1\",\"Mythbusters\":\"0\",\"Naruto\":\"0\",\"OnePiece\":\"0\",\"Pocketmon\":\"0\",\"ProperNoun\":\"-1\",\"QuotationMark\":\"0\",\"RemoveSpaces\":\"0\",\"Repeat\":\"-1\",\"RepeatAutoFix\":\"-1\",\"Smooth\":\"-1\",\"TengTong\":\"0\",\"TransliterationToTranslation\":\"0\",\"Typo\":\"-1\",\"Unit\":\"-1\",\"VioletEvergarden\":\"0\"}";
            ConvertJson.text = Str;
            ConvertJson.translateTabsToSpaces = -1;
            ConvertJson.trimTrailingWhiteSpaces = 0;
            ConvertJson.unifyLeadingHyphen = 0;
            ConvertJson.userPostReplace = "";
            ConvertJson.userPreReplace = "";
            ConvertJson.userProtectReplace = "";

            return new ChineseVariantMap().SimplifiedToTraditionalByReq(ConvertJson, ProxyCenter.CurrentProxy);
        }
        //https://docs.zhconvert.org/license/
        public string SimplifiedToTraditionalByReq(ZHConvertJson Convert,WebProxy Proxy)
        {
            try
            {
                WebHeaderCollection Headers = new WebHeaderCollection();
                Headers.Add("sec-ch-ua", "Not(A:Brand\";v=\"8\", \"Chromium\";v=\"144\", \"Google Chrome\";v=\"144");
                Headers.Add("sec-ch-ua-mobile", "?0");
                Headers.Add("Origin", "https://zhconvert.org");
                Headers.Add("Sec-Fetch-Site", "same-site");
                Headers.Add("Sec-Fetch-Mode", "cors");
                Headers.Add("Sec-Fetch-Dest", "empty");
                Headers.Add("Accept-Language", "en-GB,en-US;q=0.9,en;q=0.8,zh-CN;q=0.7,zh;q=0.6");

                HttpItem Http = new HttpItem()
                {
                    URL = "https://api.zhconvert.org/convert",
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/144.0.0.0 Safari/537.36",
                    Method = "Post",
                    Header = Headers,
                    Accept = "*/*",
                    Postdata = JsonConvert.SerializeObject(Convert),
                    Cookie = "",
                    ContentType = "application/json",
                    Referer = "https://zhconvert.org/",
                    Encoding = Encoding.UTF8,
                    MaximumResponseBytes = JsonPayload.MaximumDocumentBytes,
                    WebProxy = Proxy
                };

                try
                {
                    Http.Header.Add("Accept-Encoding", "gzip, deflate, br, zstd");
                }
                catch { }

                string GetResult = HttpTransport.GetHtml(Http).Html;

                ZHConvertReturnJson GetReturn;
                if (!TryParseResponse(GetResult, out GetReturn))
                    return string.Empty;

                if (GetReturn.data != null && GetReturn.code == 0)
                {
                    if (GetReturn.data.text != null)
                    {
                        if (GetReturn.data.text.Length > 0)
                        {
                            return GetReturn.data.text;
                        }
                    }
                }
            }
            catch { }

            return "";
        }

        /// <summary>Parses a bounded conversion response and validates its required text field.</summary>
        /// <param name="json">The untrusted provider response.</param>
        /// <param name="result">Receives the validated response, or <c>null</c> on failure.</param>
        /// <returns><c>true</c> when the provider reports success with non-empty converted text.</returns>
        internal static bool TryParseResponse(string json, out ZHConvertReturnJson result)
        {
            return JsonPayload.TryDeserialize(
                json,
                value => value != null &&
                    value.code == 0 &&
                    value.data != null &&
                    !string.IsNullOrWhiteSpace(value.data.text),
                out result);
        }

        public string SimplifiedToTraditional(ZHConvertJson Convert)
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



    public class ZHConvertReturnJson
    {
        public int code { get; set; }
        public ZHConvertData data { get; set; }
        public string msg { get; set; }
        public ZHConvertRevisions revisions { get; set; }
        public float execTime { get; set; }
    }

    public class ZHConvertData
    {
        public string converter { get; set; }
        public string text { get; set; }
        public object diff { get; set; }
        public string textFormat { get; set; }
        public object[] usedModules { get; set; }
        public object[] jpTextStyles { get; set; }
    }

    public class ZHConvertRevisions
    {
        public string build { get; set; }
        public string msg { get; set; }
        public int time { get; set; }
    }


    public class ZHConvertJson
    {
        public string text { get; set; }
        public string apiKey { get; set; }
        public string ignoreTextStyles { get; set; }
        public string jpTextStyles { get; set; }
        public string jpTextConversionStrategy { get; set; }
        public string jpStyleConversionStrategy { get; set; }
        public string modules { get; set; }
        public string userPostReplace { get; set; }
        public string userPreReplace { get; set; }
        public string userProtectReplace { get; set; }
        public int diffCharLevel { get; set; }
        public int diffContextLines { get; set; }
        public int diffEnable { get; set; }
        public int diffIgnoreCase { get; set; }
        public int diffIgnoreWhiteSpaces { get; set; }
        public string diffTemplate { get; set; }
        public int cleanUpText { get; set; }
        public int ensureNewlineAtEof { get; set; }
        public int translateTabsToSpaces { get; set; }
        public int trimTrailingWhiteSpaces { get; set; }
        public int unifyLeadingHyphen { get; set; }
        public string converter { get; set; }
    }


}
