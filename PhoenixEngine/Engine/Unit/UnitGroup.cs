using System.Collections.Generic;
using PhoenixEngine.Sequence;
using System.Text.RegularExpressions;
using PhoenixEngine.Events;
using PhoenixEngine.Translate;
using PhoenixEngine.Common;

namespace PhoenixEngine.Unit
{
    public class NeedConfirm
    {
        public int Index = 0;
        public string Result = "";
        public NeedConfirm(int Index, string Result)
        {
            this.Index = Index;
            this.Result = Result;
        }
    }
    public class ConfirmPasser
    {
        private List<BaseUnit> ParentRef = new List<BaseUnit>();
        public List<BaseUnit> Units = new List<BaseUnit>();
        public List<NeedConfirm> NeedConfirms = new List<NeedConfirm>();

        public bool Empty = false;

        public ConfirmPasser(List<BaseUnit> Units)
        {
            ParentRef = Units;
            this.Units.AddRange(Units);
        }
        public bool TryPass(ref List<BaseUnit> NotPassUnits, ref List<BaseUnit> PassUnits,bool IsDeepL)
        {
            if (this.Empty == true)
            {
                return true;
            }

            for (int i = 0; i < Units.Count; i++)
            {
                for (int ir = 0; ir < NeedConfirms.Count; ir++)
                {
                    if (i == NeedConfirms[ir].Index)
                    {
                        this.Units[i].Translated = NeedConfirms[ir].Result;

                        if (IsDeepL)
                        {
                            this.Units[i].Translated = this.Units[i].Translated.TrimEnd('\r', '\n');
                        }
                    }
                }
            }

            NotPassUnits = new List<BaseUnit>();

            for (int i = 0; i < Units.Count; i++)
            {
                var GetUnit = Units[i];
                if (GetUnit.Original.Length > 0)
                {
                    if (GetUnit.Translated.Length == 0)
                    {
                        NotPassUnits.Add(GetUnit);
                    }
                    else
                    {
                        PassUnits.Add(GetUnit);
                    }
                }
            }

            if (NotPassUnits.Count > 0)
            {
                return false;
            }
            return true;
        }

        public void Apply(List<BaseUnit> PassUnits)
        {
            for (int i = 0; i < ParentRef.Count; i++)
            {
                ParentRef[i].Translated = PassUnits[i].Translated;
            }
        }
    }

    public class UnitGroup
    {
        public string Key = "";
        public List<BaseUnit> Units = new List<BaseUnit>();
        public AggregationMode Mode = AggregationMode.Null;

        public UnitGroup LinkTo = null;

        public bool IsUnrelated = false;
        public volatile bool IsMemoryCreated = false;

        public bool IsLeaderMemoryReady()
        {
            UnitGroup Current = this;
            while (Current.LinkTo != null)
            {
                if (!Current.LinkTo.IsMemoryCreated)
                    return false;
                Current = Current.LinkTo;
            }
            return true;
        }

        public void BatchProc(int State)
        {
            if (State == 0)
            {
                for (int i = 0; i < Units.Count; i++)
                {
                    var GetUnit = Units[i];
                }
            }
            else
            {
                for (int i = 0; i < Units.Count; i++)
                {
                    var GetUnit = Units[i];
                }
            }
        }

        public UnitGroup()
        {

        }

        public UnitGroup(Translator TranslatorRef, BaseUnit SingleUnit)
        {
            Init(TranslatorRef, 0, SingleUnit, AggregationMode.Single);
        }

        public BaseUnit GetFrist()
        {
            if (Units.Count > 0)
            {
                return Units[0];
            }
            return null;
        }

        public void Init(Translator TranslatorRef, int Key,BaseUnit First, AggregationMode SetMode)
        {
            this.Mode = SetMode;

            if (SetMode == AggregationMode.Aggregation)
            {
                this.Key = Key.ToString();
                Units.Add(First);
            }
            else
            if (SetMode == AggregationMode.Single)
            {
                this.Key = First.Key;
                Units.Add(First);
            }
        }

        public void AddUnit(BaseUnit Unit)
        {
            Units.Add(Unit);
        }

        public string GenContent(ref bool CanTrans)
        {
            return UnitGroup.GenContent(this.Units,ref CanTrans);
        }

        public static string GenContent(List<BaseUnit> Array,ref bool CanTrans)
        {
            CanTrans = false;
            string Html = "";
            for (int i = 0; i < Array.Count; i++)
            {
                if (Array[i].Translated.Length == 0)
                {
                    Html += string.Format("<li data-unit-id='{0}'>{1}</li>\n", i + 100, Array[i].Original);
                    CanTrans = true;
                }
            }
            return Html;
        }

        public ConfirmPasser AnalysisContent(string Content)
        {
            ConfirmPasser WaitConfirm = new ConfirmPasser(this.Units);

            if (Content == "<empty>")
            {
                WaitConfirm.Empty = true;
            }     

            string Pattern = @"<li[^>]*data-unit-id\s*=\s*'(\d+)'[^>]*>\s*([\s\S]*?)(?=\s*</li>|\s*<li|\z)";
            var Matches = Regex.Matches(
                Content,
                Pattern,
                RegexOptions.IgnoreCase  
            );

            foreach (Match match in Matches)
            {
                int ID = P_Convert.ObjToInt(match.Groups[1].Value);
                string Result = match.Groups[2].Value.Trim();
                if (ID >= 100)
                {
                    int NormalID = ID - 100;
                    if (NormalID >= 0)
                    {
                        WaitConfirm.NeedConfirms.Add(
                            new NeedConfirm(NormalID, Result)
                        );
                    }
                }
            }

            return WaitConfirm;
        }

        public GroupContext ApplyStateChange(string TranslatorID, UnitTranslationState State)
        {
            GroupContext GenContent = new GroupContext();

            if (EngineEvents.SetBaseUnitStateChangedCallback != null)
                for (int i = 0; i < this.Units.Count; i++)
                {
                    UnitContext<BaseUnit> Data = this.Units[i].ApplyStateChange(TranslatorID,State);
                    if (Data != null)
                    {
                        GenContent.AddSign(Data.Key, Data.ControlSignal);
                    }
                }

            return GenContent;
        }
    }
}