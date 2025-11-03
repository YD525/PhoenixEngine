
using System.Text.RegularExpressions;
using System.Transactions;

namespace PhoenixEngine.TranslateManage
{
    public class JsonGeter
    {
        public static string GetValue(string Json,string Name = "translation")
        {
            if (string.IsNullOrEmpty(Json))
                return string.Empty;

            var Matches = Regex.Matches(Json, @"[""']?\s*" + Regex.Escape(Name) + @"\s*[""']?\s*:\s*[""'](.*?)[""']", RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (Matches.Count > 0)
            {
                return Matches[Matches.Count - 1].Groups[1].Value.Trim();
            }

            return string.Empty;
        }
    }
}
