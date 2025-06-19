using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PhoenixEngine.TranslateManage;

namespace PhoenixEngine.DelegateManagement
{
    public class DelegateHelper
    {
        public static LogCall ?SetLog = null;
        public delegate bool LogCall(string Log,int LogViewType);

        #region DashBoard

        public static SetOutput? SetOutputCall = null;

        public delegate void SetOutput(string Str);

        public static SetKeyWords? SetKeyWordsCall = null;

        public delegate void SetKeyWords(List<ReplaceTag> KeyWords);
        
        public static SetUsage? SetUsageCall = null;

        public delegate void SetUsage(PlatformType Type, int Count);
        #endregion

    }
}
