using System.Net;
using System.Text.Json;
using System.Web;
using PhoenixEngine.DelegateManagement;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.RequestManagement;
using PhoenixEngine.TranslateCore;

namespace PhoenixEngine.PlatformManagement
{
    public class GoogleTransApi
    {
        private static readonly HttpClient _httpClient = CreateHttpClient();
        private static HttpClient CreateHttpClient()
        {
            try
            {
                if (ProxyCenter.GlobalProxyIP.Trim().Length > 0)
                {
                    var Proxy = new WebProxy(ProxyCenter.GlobalProxyIP, false);

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
        public string Translate(string text, Languages targetLanguage, Languages? sourceLanguage = null)
        {

            try
            {
                string targetCode = LanguageHelper.ToLanguageCode(targetLanguage);
                string sourceCode = sourceLanguage.HasValue ? LanguageHelper.ToLanguageCode(sourceLanguage.Value) : "auto";

                string url = $"https://translation.googleapis.com/language/translate/v2" +
                             $"?key={EngineConfig.GoogleApiKey}" +
                             $"&q={HttpUtility.UrlEncode(text)}" +
                             $"&target={targetCode}" +
                             $"&source={sourceCode}";

                HttpResponseMessage response = _httpClient.GetAsync(url).Result;
                response.EnsureSuccessStatusCode();

                string json = response.Content.ReadAsStringAsync().Result;

                if (DelegateHelper.SetLog != null)
                {
                    DelegateHelper.SetLog("GoogleApi:" + json,1);
                }

                using JsonDocument doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("data", out JsonElement dataElem) &&
                    dataElem.TryGetProperty("translations", out JsonElement translationsElem) &&
                    translationsElem.GetArrayLength() > 0 &&
                    translationsElem[0].TryGetProperty("translatedText", out JsonElement textElem))
                {
                    return textElem.GetString() ?? string.Empty;
                }

                return string.Empty;
            }
            catch { return string.Empty; }
        }
    }
}
