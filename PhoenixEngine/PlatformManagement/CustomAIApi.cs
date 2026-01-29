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
    public class CustomAIApi : I_AI_TranslationNode
    {
        public static PlatformType Type = PlatformType.CustomPlatform;

        public CustomPlatformType CustomType = CustomPlatformType.CloudAI;

        public CustomCore Core = new CustomCore();
        public string ApiKey { get; set; } = "";
        public string Model { get; set; } = "";
        public AITranslationMemory AIMemoryRef { get; set; } = null;
        public EngineConfigJson ConfigRef { get; set; } = null;
        public WebProxy ProxyRef { get; set; } = null;

        public void Init(AITranslationMemory AIMemory, EngineConfigJson Config, WebProxy Proxy)
        { 
            this.AIMemoryRef = AIMemory;
            this.ConfigRef = Config;
            this.ProxyRef = Proxy;
        }
        public void SetApiKey(string Key)
        { 
           this.ApiKey = Key;
        }

        public string QuickTrans(List<ReplaceTag> CustomWords, string TransSource, Languages FromLang, Languages ToLang, bool UseAIMemory, int AIMemoryCountLimit, string AIParam, ref AICall Call, string Type)
        {
            return string.Empty;
        }
    }
}
