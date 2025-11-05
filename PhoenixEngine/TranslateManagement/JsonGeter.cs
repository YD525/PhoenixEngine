
using System.Text.RegularExpressions;
using System.Transactions;
using PhoenixEngine.ConvertManager;

namespace PhoenixEngine.TranslateManage
{
    public class JsonGeter
    {
        public static string GetValue(string Json, string Name = "translation")
        {
            if (string.IsNullOrEmpty(Json))
                return string.Empty;

            string GetStr = ConvertHelper.StringDivision(Json, Name, "}");

            if (GetStr.Contains(":"))
            {
                GetStr = GetStr.Split(':')[1].Trim();
                GetStr = GetStr.Trim('"');
                return GetStr;
            }

            return string.Empty;
        }
    }
}
