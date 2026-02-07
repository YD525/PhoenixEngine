using PhoenixEngine.EngineManagement.Unit;

namespace PhoenixEngine.TranslateManagement
{
    public class BaseUnit
    {
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

            if (this.RealOriginal.Length == 0)
            {
                this.RealOriginal = string.Copy(Original);
            }
           
            this.Original = Original;
            this.Translated = Translated;
            this.Score = Score;
        }

        public BaseUnit Clone(BaseUnit Unit)
        {
            return new BaseUnit
            {
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

        public string GetRealOriginal()
        {
            return string.Copy(this.RealOriginal);
        }
       
    }

}
