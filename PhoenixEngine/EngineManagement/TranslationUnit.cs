using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using PhoenixEngine.DelegateManagement;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.TranslateManage;
using static PhoenixEngine.TranslateManage.EngineCore;

namespace PhoenixEngine.TranslateManagement
{
    public class TranslationUnit
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
        public TranslationUnit(int FileUniqueKey, string Key, string Type, string Original, string Translated, double Score)
        {
            this.FileUniqueKey = FileUniqueKey;
            this.Key = Key;
            this.Type = Type;
            this.Original = Original;
            this.Translated = Translated;
            this.Score = Score;
        }
    }

}
