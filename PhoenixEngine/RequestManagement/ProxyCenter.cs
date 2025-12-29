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
        public static WebProxy CurrentProxy = null;

        public static void UsingProxy()
        {
            if (!string.IsNullOrWhiteSpace(EngineConfig.Config.ProxyUrl))
            {
                WebProxy NewProxy = new WebProxy(EngineConfig.Config.ProxyUrl);

                if (!string.IsNullOrEmpty(EngineConfig.Config.ProxyUserName) &&
               !string.IsNullOrEmpty(EngineConfig.Config.ProxyPassword))
                {
                    NewProxy.Credentials = new NetworkCredential(
                        EngineConfig.Config.ProxyUserName,
                        EngineConfig.Config.ProxyPassword
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
