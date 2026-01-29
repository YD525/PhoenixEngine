using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManage;
using static PhoenixEngine.EngineManagement.DataTransmission;

namespace PhoenixEngine.PlatformManagement
{
    public interface I_AITranslationNode
    {
        string QuickTrans(List<ReplaceTag> CustomWords, string TransSource, Languages FromLang, Languages ToLang, bool UseAIMemory, int AIMemoryCountLimit, string AIParam, ref AICall Call, string Type);
        void SetApiKey(string Key);
    }

    public interface I_TranslationNode
    {
        string QuickTrans(string TransSource, Languages FromLang, Languages ToLang, ref PlatformCall Call);
        void SetApiKey(string Key);
    }
}
