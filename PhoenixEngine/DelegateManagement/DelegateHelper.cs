using PhoenixEngine.EngineManagement.Unit;

namespace PhoenixEngine.DelegateManagement
{
    public class DelegateHelper
    {
        public static SetData SetDataCall = null;
        public delegate void SetData(int Sign,object Any);

        public static TranslationUnitCallBack SetTranslationUnitCallBack = null;
        public delegate bool TranslationUnitCallBack(UnitGroup Item,int State);

        public static BookTranslateCallback SetBookTranslateCallback = null;

        public delegate void BookTranslateCallback(string Key,string CurrentText);

    }
}
