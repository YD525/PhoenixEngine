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
    public interface I_BaseNode
    {
        string ApiKey { get; set; }
        AITranslationMemory AIMemoryRef { get; set; }
        EngineConfigJson ConfigRef { get; set; }
        void SetApiKey(string Key);
        WebProxy ProxyRef { get; set; }
        void Init(AITranslationMemory AIMemory, EngineConfigJson Config, WebProxy Proxy);
    }

    public interface I_AITranslationNode: I_BaseNode
    {
        string Model { get; set; }
        string QuickTrans(List<ReplaceTag> CustomWords, string TransSource, Languages FromLang, Languages ToLang, bool UseAIMemory, int AIMemoryCountLimit, string AIParam, ref AICall Call, string Type);
    }

    public interface I_TranslationNode: I_BaseNode
    {
        string QuickTrans(string TransSource, Languages FromLang, Languages ToLang, ref PlatformCall Call);
    }
}
