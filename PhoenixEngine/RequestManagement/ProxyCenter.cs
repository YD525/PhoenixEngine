using System.Net;
using PhoenixEngine.EngineManagement;

namespace PhoenixEngine.RequestManagement
{
    public class ProxyCenter
    {
        public static WebProxy CurrentProxy = null;

        public static void UsingProxy()
        {
            if (!string.IsNullOrWhiteSpace(Phoenix.Config.ProxyUrl))
            {
                WebProxy NewProxy = new WebProxy(Phoenix.Config.ProxyUrl);

                if (!string.IsNullOrEmpty(Phoenix.Config.ProxyUserName) &&
               !string.IsNullOrEmpty(Phoenix.Config.ProxyPassword))
                {
                    NewProxy.Credentials = new NetworkCredential(
                        Phoenix.Config.ProxyUserName,
                        Phoenix.Config.ProxyPassword
                    );
                }

                CurrentProxy = NewProxy;
            }
            else
            {
                CurrentProxy = null;
            }
        }
    }
}
