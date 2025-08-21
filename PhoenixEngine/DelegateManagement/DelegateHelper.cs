using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using PhoenixEngine.TranslateManage;

namespace PhoenixEngine.DelegateManagement
{
    public class DelegateHelper
    {
        public static AICallBack? SetAICallBack = null;
        public delegate void AICallBack(string Send,string Recv,bool Sucess);

        #region DashBoard
        
        public static SetUsage? SetUsageCall = null;

        public delegate void SetUsage(PlatformType Type, int Count);

        #endregion

        public static BookTranslateCallback SetBookTranslateCallback = null;

        public delegate void BookTranslateCallback(string Key,string CurrentText);

    }
}
