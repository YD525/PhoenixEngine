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
            public Signal ControlSignal;
        }

        public class Signal
        {
            public int Sign = 0;
            public int Index = 0;
        }

        public class GroupContext
        {
            public Dictionary<string, Signal> ControlSignals = new Dictionary<string, Signal>();

            public void AddSign(string Key, Signal Item)
            {
                ControlSignals[Key] = Item;
            }

            public bool CanDo(int Signal)
            {
                foreach (var Get in this.ControlSignals)
                {
                    if (Get.Value.Sign == Signal)
                    {
                        return false;
                    }
                }

                return true;
            }
            public bool CanDo(int Signal,ref int Index)
            {
                foreach (var Get in this.ControlSignals)
                {
                    if (Get.Value.Sign == Signal)
                    {
                        Index = Get.Value.Index;
                        return false;
                    }
                }

                return true;
            }
        }

        public static SetData SetDataCall = null;
        public delegate void SetData(int Sign,object Any);

        public static OnUnitStateChanged SetBaseUnitStateChangedCallback = null;
        public delegate UnitContext<BaseUnit> OnUnitStateChanged(BaseUnit Item, UnitTranslationState State);

        public static BookTranslateCallback SetBookTranslateCallback = null;

        public delegate void BookTranslateCallback(string Key,string CurrentText);

    }

    public enum UnitTranslationState
    {
        None = 0,
        Created = 1,               
        Preparing = 3,
        Skipped = 5,
        Translating = 6,      
        Completed = 7,
        Queued = 8,
        Failed = 9 
    }
}
