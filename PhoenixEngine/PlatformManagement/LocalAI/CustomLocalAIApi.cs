using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PhoenixEngine.TranslateManage;

namespace PhoenixEngine.PlatformManagement
{
    public class CustomLocalAIApi : I_Local_AI_TranslationNode
    {
        public static PlatformType Type = PlatformType.CustomPlatform;

        public CustomPlatformType CustomType = CustomPlatformType.LocalAI;
    }
}
