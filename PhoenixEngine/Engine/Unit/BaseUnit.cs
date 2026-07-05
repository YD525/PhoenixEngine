using PhoenixEngine.Engine;
using System.Collections.Generic;
using PhoenixEngine.Events;
using PhoenixEngine.Translate;

namespace PhoenixEngine.Unit
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

        public UnitContext<BaseUnit> ApplyStateChange(string TranslatorID, UnitTranslationState State)
        {
            if (EngineEvents.SetBaseUnitStateChangedCallback != null)
            {
                var Mutation = EngineEvents.SetBaseUnitStateChangedCallback(TranslatorID,Clone(this),State);

                if (Mutation != null)
                {
                    if (Mutation.ControlSignal.Sign == 1)
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
                }

                Mutation.Key = this.Key;

                return Mutation;
            }

            return new UnitContext<BaseUnit>();
        }

        public void ReSet()
        {
            this.Original = this.RealOriginal;
        }
       
    }

    public static class BaseUnitExtend
    {
        public static HashSet<string> ExtractTokens(this BaseUnit Unit,Translator TranslatorRef)
        {
            return TextTokenizer.BuildTokenSignature(TranslatorRef.From, Unit.Original, 0);
        }
    }

}
