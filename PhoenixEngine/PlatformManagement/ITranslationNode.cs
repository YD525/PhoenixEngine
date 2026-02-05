using System.Collections.Generic;
using System.Net;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManage;
using static PhoenixEngine.EngineManagement.DataTransmission;

namespace PhoenixEngine.PlatformManagement
{
    public interface I_AI_TranslationNode
    {
        AITranslationMemory AIMemoryRef { get; set; }
        EngineConfigJson ConfigRef { get; set; }
        WebProxy ProxyRef { get; set; }
        int CustomID { get; set; }
        void Init(int CustomID,AITranslationMemory AIMemory, EngineConfigJson Config, WebProxy Proxy);
        string Model { get; set; }
        string QuickTrans(string ApiKey,List<ReplaceTag> CustomWords, string TransSource, Languages FromLang, Languages ToLang, bool UseAIMemory, int AIMemoryCountLimit, string AIParam, ref AICall Call, string Type);
    }

    public interface I_Local_AI_TranslationNode
    {
        AITranslationMemory AIMemoryRef { get; set; }
        EngineConfigJson ConfigRef { get; set; }
        int LocalPort { get; set; }
        int CustomID { get; set; }
        void Init(int CustomID, AITranslationMemory AIMemory, EngineConfigJson Config);
        string QuickTrans(List<ReplaceTag> CustomWords, string TransSource, Languages FromLang, Languages ToLang, bool UseAIMemory, int AIMemoryCountLimit, string AIParam, ref AICall Call, string Type);
    }

    public interface I_TranslationNode
    {
        EngineConfigJson ConfigRef { get; set; }
        WebProxy ProxyRef { get; set; }
        int CustomID { get; set; }
        void Init(int CustomID,EngineConfigJson Config, WebProxy Proxy);
        string QuickTrans(string ApiKey,string TransSource, Languages FromLang, Languages ToLang, ref PlatformCall Call);
    }
}
