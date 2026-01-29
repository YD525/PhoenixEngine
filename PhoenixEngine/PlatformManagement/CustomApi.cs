using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManage;
using static PhoenixEngine.EngineManagement.DataTransmission;

namespace PhoenixEngine.PlatformManagement
{
    public class CustomApi : I_TranslationNode
    {
        public static PlatformType Type = PlatformType.CustomPlatform;

        public CustomPlatformType CustomType = CustomPlatformType.Traditional;

        public CustomReqCore Core = new CustomReqCore();
        public string ApiKey { get; set; } = "";
        public EngineConfigJson ConfigRef { get; set; } = null;
        public WebProxy ProxyRef { get; set; } = null;
        public void SetApiKey(string Key)
        { 
            this.ApiKey = Key;
        }
        public int CustomID { get; set; } = 0;
        public void Init(int CustomID,EngineConfigJson Config, WebProxy Proxy)
        { 
            this.CustomID = CustomID;
            this.ConfigRef = Config;
            this.ProxyRef = Proxy;
        }

        public string QuickTrans(string TransSource, Languages FromLang, Languages ToLang, ref PlatformCall Call)
        {
            return "";
        }
    }
}
