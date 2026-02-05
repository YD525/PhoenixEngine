using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using PhoenixEngine.DelegateManagement;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.TranslateManage;
using static PhoenixEngine.TranslateManage.EngineCore;

namespace PhoenixEngine.TranslateManagement
{
    public class T_BaseUnit
    {
        public TranslationUnitGroup ParentRef = null;
        public int FileUniqueKey = 0;
        public double Score = 100;
        public string Key = "";
        public string Type = "";
        public string Original = "";
        public string Translated = "";
        public bool Leader = false;
        public double TempSim = 0;

        public T_BaseUnit() { }
        public T_BaseUnit(int FileUniqueKey, string Key, string Type, string Original, string Translated, double Score)
        {
            this.FileUniqueKey = FileUniqueKey;
            this.Key = Key;
            this.Type = Type;
            this.Original = Original;
            this.Translated = Translated;
            this.Score = Score;
        }

        public BaseUnit Clone(BaseUnit Unit)
        {
            return new BaseUnit
            {
                ParentRef = this.ParentRef, //Int ptr 
                FileUniqueKey = this.FileUniqueKey,
                Score = this.Score,
                Key = this.Key,
                Type = this.Type,
                Original = this.Original,
                Translated = this.Translated,
                Leader = this.Leader,
                TempSim = this.TempSim
            };
        }
    }

}
