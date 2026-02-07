using System.Collections.Generic;
using PhoenixEngine.EngineManagement.Unit;
using PhoenixEngine.TranslateManagement;

namespace PhoenixEngine.DelegateManagement
{
    public class EngineEvents
    {
        public class UnitContext<T>
        {
            public T Data;
            public string Key = "";
            public int ControlSignal = 0;
        }

        public class GroupContext
        {
            public Dictionary<string,int> ControlSignals = new Dictionary<string, int>();

            public void AddSign(string Key,int Signal)
            {
                ControlSignals[Key] = Signal;
            }

            public bool CanDo(int Signal)
            {
                foreach (var Get in this.ControlSignals)
                {
                    if (Get.Value != Signal)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public static SetData SetDataCall = null;
        public delegate void SetData(int Sign,object Any);

        public static OnUnitStateChanged BaseUnitStateChanged = null;
        public delegate UnitContext<BaseUnit> OnUnitStateChanged(BaseUnit Item, UnitTranslationState State);

        public static BookTranslateCallback SetBookTranslateCallback = null;

        public delegate void BookTranslateCallback(string Key,string CurrentText);

    }

    public enum UnitTranslationState
    {
        None = 0,
        Created = 1,          
        Queued = 2,           
        Preparing = 3,        
        Translating = 4,      
        Completed = 5,        
        Skipped = 6,          
        Failed = 7 
    }
}
