using System.Text.Json;
using System.Web;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.RequestManagement;
using PhoenixEngine.TranslateCore;
using static PhoenixEngine.EngineManagement.DataTransmission;

namespace PhoenixEngine.PlatformManagement
{
    public class GoogleTransApi
    {
        private static readonly HttpClient _HttpClient = CreateHttpClient();
        private static HttpClient CreateHttpClient()
        {
            try
            {
                if (ProxyCenter.CurrentProxy!=null)
                {
                    var Proxy = ProxyCenter.CurrentProxy;

                    var Handler = new HttpClientHandler
                    {
                        Proxy = Proxy,
                        UseProxy = true
                    };

                    return new HttpClient(Handler);
                }
                else
                {
                    return new HttpClient();
                }
            }
            catch { return new HttpClient(); }
        }
        public string Translate(string Text, Languages TargetLanguage, Languages? SourceLanguage,ref PlatformCall Call)
        {

            try
            {
                string TargetLang = LanguageHelper.ToLanguageCode(TargetLanguage);
                string SourceLang = SourceLanguage.HasValue ? LanguageHelper.ToLanguageCode(SourceLanguage.Value) : "auto";

                string Url = $"https://translation.googleapis.com/language/translate/v2" +
                             $"?key={EngineConfig.GoogleApiKey}" +
                             $"&q={HttpUtility.UrlEncode(Text)}" +
                             $"&target={TargetLang}" +
                             $"&source={SourceLang}";

                Call.PlatformName = "GoogleApi";
                Call.SendString = Url;

                HttpResponseMessage Response = _HttpClient.GetAsync(Url).Result;
                Response.EnsureSuccessStatusCode();

                string Json = Response.Content.ReadAsStringAsync().Result;

                Call.ReceiveString = Json;

                using JsonDocument Doc = JsonDocument.Parse(Json);

                if (Doc.RootElement.TryGetProperty("data", out JsonElement DataElem) &&
                    DataElem.TryGetProperty("translations", out JsonElement TranslationsElem) &&
                    TranslationsElem.GetArrayLength() > 0 &&
                    TranslationsElem[0].TryGetProperty("translatedText", out JsonElement TextElem))
                {
                    Call.Success = true;
                    return TextElem.GetString() ?? string.Empty;
                }

                return string.Empty;
            }
            catch { return string.Empty; }
        }
    }
}
