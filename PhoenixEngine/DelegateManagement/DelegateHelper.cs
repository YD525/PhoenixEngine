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
        public static SetData? SetDataCall = null;
        public delegate void SetData(int Sign,object Any);

        public static TranslationUnitCallBack? SetTranslationUnitCallBack = null;
        public delegate bool TranslationUnitCallBack(TranslationUnit Item);

        public static BookTranslateCallback SetBookTranslateCallback = null;

        public delegate void BookTranslateCallback(string Key,string CurrentText);

    }
}
