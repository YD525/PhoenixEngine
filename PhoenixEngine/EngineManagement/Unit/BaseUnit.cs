using PhoenixEngine.EngineManagement.Unit;

namespace PhoenixEngine.TranslateManagement
{
    public class BaseUnit
    {
        public UnitGroup ParentRef = null;
        public int FileUniqueKey = 0;
        public double Score = 100;
        public string Key = "";
        public string Type = "";
        private string RealOriginal = "";
        public string Original = "";
        public string Translated = "";
        public bool Leader = false;
        public double TempSim = 0;

        public BaseUnit() { }
        public BaseUnit(int FileUniqueKey, string Key, string Type, string Original, string Translated, double Score)
        {
            this.FileUniqueKey = FileUniqueKey;
            this.Key = Key;
            this.Type = Type;
            this.RealOriginal = string.Copy(Original);
            this.Original = Original;
            this.Translated = Translated;
            this.Score = Score;
        }

        private BaseUnit Clone(BaseUnit Unit)
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

        public void ReSet()
        {
            this.Original = this.RealOriginal;
            this.Translated = string.Empty;
        }
    }

}
