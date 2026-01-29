using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhoenixEngine.PlatformManagement
{
    public interface ITranslationNode
    {
        string QuickTrans();
        void SetApiKey(string Key);
    }
}
