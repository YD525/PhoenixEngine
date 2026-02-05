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
        public TranslationUnitGroup GroupRef = null;
        public int FileUniqueKey = 0;
        public double Score = 100;
        public string Key = "";
        public string Type = "";
        public string SourceText = "";
        public string TransText = "";
        public bool Leader = false;
        public double TempSim = 0;
        public TranslationUnit(int FileUniqueKey, string Key, string Type, string SourceText, string TransText,double Score)
        {
            this.FileUniqueKey = FileUniqueKey;
            this.Key = Key;
            this.Type = Type;
            this.SourceText = SourceText;
            this.TransText = TransText;
            this.Score = Score;
        }
    }

}
