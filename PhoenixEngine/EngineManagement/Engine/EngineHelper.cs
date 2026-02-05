using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PhoenixEngine.TranslateManage;
using PhoenixEngine.TranslateManagement;

namespace PhoenixEngine.EngineManagement.Engine
{
    public static class BaseUnitExtend
    {
        public static HashSet<string> ExtractTokens(this BaseUnit Unit)
        {
            return TextTokenizer.BuildTokenSignature(Phoenix.From, Unit.Original, 0);
        }
    }

    internal class EngineExtend
    {
       
    }
}
