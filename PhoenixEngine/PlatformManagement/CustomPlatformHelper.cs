using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhoenixEngine.PlatformManagement
{
    public class CustomPlatformHelper
    {
    }

    public class ReplaceTag
    {
        public string Tag { get; set; } = "";
        public string Value = "";
    }

    public enum CustomPlatformType
    { 
        Null = 0, LocalAI = 1, CloudAI = 2 ,Traditional = 3
    }
}
