using System.Collections.Generic;
using PhoenixEngine.Sequence;
using System.Text.RegularExpressions;
using PhoenixEngine.Events;
using PhoenixEngine.Translate;
using PhoenixEngine.Common;
using PhoenixEngine.Engine;
using System.Web;
using System.Text;

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

    public class HTMLGenerator
    {
        public class GenerationResult
        {
            public string Html { get; set; }
            public Dictionary<int, List<int>> TagIndexToUnitIndices { get; set; }
            public bool CanTrans { get; set; }
        }

        public GenerationResult Generate(List<BaseUnit> Units, bool IsLink,bool ShowTranslated)
        {
            Dictionary<int, List<int>> TagIndexToUnitIndices = new Dictionary<int, List<int>>();
            StringBuilder HtmlBuilder = new StringBuilder();
            Dictionary<string, int> Seen = new Dictionary<string, int>();
            int TagIndex = 100;
            bool FillerMarkerInserted = false;
            bool CanTrans = false;

            for (int i = 0; i < Units.Count; i++)
            {
                var Unit = Units[i];

                if (Unit.Original.Length == 0)
                    continue;

                if (Unit.Translated.Length > 0)
                {
                    if (!ShowTranslated || !IsLink)
                    {
                        continue;  
                    }

                    TagIndexToUnitIndices[TagIndex] = new List<int> { i };

                    if (!string.IsNullOrEmpty(Unit.Emotion))
                    {
                        HtmlBuilder.AppendFormat("<li data-unit-id='{0}' data-emotion='{1}' data-status='translated' data-original='{2}'>{3}</li>\n",
                            TagIndex, Unit.Emotion, HttpUtility.HtmlAttributeEncode(Unit.Original), Unit.Translated);
                    }
                    else
                    {
                        HtmlBuilder.AppendFormat("<li data-unit-id='{0}' data-status='translated' data-original='{1}'>{2}</li>\n",
                            TagIndex, HttpUtility.HtmlAttributeEncode(Unit.Original), Unit.Translated);
                    }

                    TagIndex++;
                    continue;
                }

                string Key = Unit.Original;

                // Similarity buckets always use deduplication.
                // Relationship buckets require ForceContextDeduplication.
                bool EnableDeduplication = !IsLink || Phoenix.Config.ForceContextDeduplication;

                bool IsDuplicate = EnableDeduplication && Seen.ContainsKey(Key);

                if (!IsDuplicate)
                {
                    if (EnableDeduplication)
                    {
                        Seen[Key] = TagIndex;
                    }
                    TagIndexToUnitIndices[TagIndex] = new List<int> { i };

                    if (!FillerMarkerInserted && Unit.IsFilled)
                    {
                        HtmlBuilder.Append("<!-- next content section -->\n");
                        FillerMarkerInserted = true;
                    }

                    if (!string.IsNullOrEmpty(Unit.Emotion))
                    {
                        HtmlBuilder.AppendFormat("<li data-unit-id='{0}' data-emotion='{1}'>{2}</li>\n",
                            TagIndex, Unit.Emotion, Unit.Original);
                    }
                    else
                    {
                        HtmlBuilder.AppendFormat("<li data-unit-id='{0}'>{1}</li>\n",
                            TagIndex, Unit.Original);
                    }

                    CanTrans = true;
                    TagIndex++;
                }
                else
                {
                    int ExistingTagIndex = Seen[Key];
                    TagIndexToUnitIndices[ExistingTagIndex].Add(i);
                }
            }

            return new GenerationResult
            {
                Html = HtmlBuilder.ToString(),
                TagIndexToUnitIndices = TagIndexToUnitIndices,
                CanTrans = CanTrans
            };
        }
    }
    public class ConfirmPasser
    {
        private List<BaseUnit> ParentRef = new List<BaseUnit>();
        public List<BaseUnit> Units = new List<BaseUnit>();
        public List<NeedConfirm> NeedConfirms = new List<NeedConfirm>();

        public bool Empty = false;

        private Dictionary<int, List<int>> TagIndexToUnitIndices = new Dictionary<int, List<int>>();

        public ConfirmPasser(List<BaseUnit> Units)
        {
            ParentRef = Units;
            this.Units.AddRange(Units);
        }

        public string GenContent(ref bool CanTrans,bool IsLink,bool ShowTranslated)
        {
            HTMLGenerator Generator = new HTMLGenerator();
            var Result = Generator.Generate(Units, IsLink, ShowTranslated); 

            this.TagIndexToUnitIndices = Result.TagIndexToUnitIndices;
            CanTrans = Result.CanTrans;

            return Result.Html;
        }

        public bool TryPass(ref List<BaseUnit> NotPassUnits, ref List<BaseUnit> PassUnits, bool IsDeepL)
        {
            if (this.Empty)
            {
                return true;
            }

            foreach (var NeedConfirm in NeedConfirms)
            {
                int TagIndex = NeedConfirm.Index;

                if (TagIndexToUnitIndices.TryGetValue(TagIndex, out var UnitIndices))
                {
                    string Translated = NeedConfirm.Result;

                    if (IsDeepL)
                    {
                        Translated = Translated.TrimEnd('\r', '\n');
                    }

                    foreach (int UnitIndex in UnitIndices)
                    {
                        if (UnitIndex >= 0 && UnitIndex < Units.Count)
                        {
                            Units[UnitIndex].Translated = Translated;
                        }
                    }
                }
            }

            NotPassUnits = new List<BaseUnit>();
            PassUnits = new List<BaseUnit>();

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
                if (i < PassUnits.Count)
                {
                    ParentRef[i].Translated = PassUnits[i].Translated;
                }
            }
        }

        public ConfirmPasser AnalysisContent(string Content)
        {
            ConfirmPasser WaitConfirm = this;

            if (Content == "<empty>")
            {
                WaitConfirm.Empty = true;
                return WaitConfirm;
            }

            string Pattern = @"<li[^>]*data-unit-id\s*=\s*'(\d+)'[^>]*>\s*([\s\S]*?)(?=\s*</li>|\s*<li|\z)";
            var Matches = Regex.Matches(Content, Pattern, RegexOptions.IgnoreCase);

            foreach (Match Match in Matches)
            {
                int ID = P_Convert.ObjToInt(Match.Groups[1].Value);
                string Result = Match.Groups[2].Value.Trim();
                if (ID >= 100)
                {
                    WaitConfirm.NeedConfirms.Add(
                        new NeedConfirm(ID, Result)
                    );
                }
            }

            return WaitConfirm;
        }
    }

    public class UnitGroup
    {
        public string Key = "";
        public List<BaseUnit> Units = new List<BaseUnit>();
        public AggregationMode Mode = AggregationMode.Null;
        public bool IsLink = false;

        public P_Bucket Bucket = null;

        public bool IsUnrelated = false;
        public volatile bool IsMemoryCreated = false;

        public ConfirmPasser ConfirmPasser = null;

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

        public void Init(Translator TranslatorRef, int Key, BaseUnit First, AggregationMode SetMode)
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

        private void SetConfirmPasser()
        {
            this.ConfirmPasser = new ConfirmPasser(this.Units);
        }

        public string GenContent(ref bool CanTrans)
        {
            SetConfirmPasser();
            return ConfirmPasser.GenContent(ref CanTrans,this.IsLink,Phoenix.Config.PreserveConversationContext);
        }

        public ConfirmPasser AnalysisContent(string Content)
        {
            return ConfirmPasser.AnalysisContent(Content);
        }

        public GroupContext ApplyStateChange(string TranslatorID, UnitTranslationState State)
        {
            GroupContext GenContent = new GroupContext();

            if (EngineEvents.SetBaseUnitStateChangedCallback != null)
                for (int i = 0; i < this.Units.Count; i++)
                {
                    UnitContext<BaseUnit> Data = this.Units[i].ApplyStateChange(TranslatorID, State);
                    if (Data != null)
                    {
                        GenContent.AddSign(Data.Key, Data.ControlSignal);
                    }
                }

            return GenContent;
        }
    }
}