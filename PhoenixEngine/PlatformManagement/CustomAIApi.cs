using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PhoenixEngine.TranslateManage;

namespace PhoenixEngine.PlatformManagement
{
    public class CustomAIApi : I_AI_TranslationNode
    {
        public static PlatformType Type = PlatformType.CustomPlatform;

        public CustomPlatformType CustomType = CustomPlatformType.CloudAI;

        public CustomCore Core = new CustomCore();
    }
}
