using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using PhoenixEngine.EngineManagement;

namespace PhoenixEngine.RequestManagement
{
    public class ProxyCenter
    {
        public static WebProxy? CurrentProxy = null;

        public static void UsingProxy()
        {
            if (!string.IsNullOrWhiteSpace(EngineConfig.ProxyUrl))
            {
                WebProxy NewProxy = new WebProxy(EngineConfig.ProxyUrl);

                if (!string.IsNullOrEmpty(EngineConfig.ProxyUserName) &&
               !string.IsNullOrEmpty(EngineConfig.ProxyPassword))
                {
                    NewProxy.Credentials = new NetworkCredential(
                        EngineConfig.ProxyUserName,
                        EngineConfig.ProxyPassword
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
