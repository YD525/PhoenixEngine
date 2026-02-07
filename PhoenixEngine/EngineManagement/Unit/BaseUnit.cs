using PhoenixEngine.DelegateManagement;
using PhoenixEngine.EngineManagement.Unit;
using static PhoenixEngine.DelegateManagement.EngineEvents;

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

        public UnitContext<BaseUnit> ApplyStateChange(UnitTranslationState State)
        {
            if (EngineEvents.BaseUnitStateChanged != null)
            {
                var Mutation = EngineEvents.BaseUnitStateChanged(Clone(this),State);

                if (Mutation != null)
                {
                    if (Mutation.Data.Original.Length > 0)
                    { 
                        this.Original = Mutation.Data.Original;
                    }
                    if (!string.IsNullOrEmpty(Mutation.Data.Translated))
                    {
                        this.Translated = Mutation.Data.Translated;
                    }
                    if (Mutation.Data.Type.Length > 0)
                    {
                        this.Type = Mutation.Data.Type;
                    }
                }

                Mutation.Key = this.Key;

                return Mutation;
            }

            return null;
        }
       
    }

}
