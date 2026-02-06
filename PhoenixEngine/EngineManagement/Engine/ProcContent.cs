using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PhoenixEngine.EngineManagement.Sequence;
using PhoenixEngine.EngineManagement.Unit;
using PhoenixEngine.GameManagement;
using PhoenixEngine.TranslateManage;
using PhoenixEngine.TranslateManagement;

namespace PhoenixEngine.EngineManagement.Engine
{
    public class ProcContent
    {
        public static int TextLengthLimit = 2000;

        public int GenKey = 0;

        private Translator TranslatorRef = null;

        public List<UnitGroup> Units = new List<UnitGroup>();
        public List<UnitGroup> SameItems = new List<UnitGroup>();
        public List<UnitGroup> Books = new List<UnitGroup>();

        public UnionArray UnionData = null;
        public ProcContent(Translator Translator)
        {
            this.TranslatorRef = Translator;
        }
        public Translator GetTranslator()
        {
            return this.TranslatorRef;
        }
        private void AddLeader(BaseUnit Item)
        {
            GenKey++;

            UnitGroup BatchUnit = new UnitGroup(this);
            BatchUnit.Init(GenKey, Item, AggregationMode.Aggregation);
            Units.Add(BatchUnit);
        }
        private void Add(BaseUnit Item)
        {
            GenKey++;

            Game CheckGameType = Game.Null;
            if (SkyrimBookHelper.IsSkyrimBook(Item, ref CheckGameType))
            {
                Books.Add(new UnitGroup(this, Item));
                return;
            }

            var GenTokens = Item.ExtractTokens();

            foreach (var GetBatchUnit in this.Units)
            {
                if (GetBatchUnit.IsSimilarTo(GenTokens, 1))
                {
                    if (GetBatchUnit.TotalLength < TextLengthLimit)
                    {
                        GetBatchUnit.AddUnit(Item, GenTokens);
                    }
                    else
                    {
                        UnitGroup NextBatchUnit = new UnitGroup(this);

                        NextBatchUnit.Key = GenKey.ToString();

                        NextBatchUnit.AnchorTokens = new HashSet<string>(GetBatchUnit.AnchorTokens);
                        NextBatchUnit.AllTokens = new HashSet<string>(NextBatchUnit.AnchorTokens);

                        NextBatchUnit.AddUnit(Item, GenTokens);

                        NextBatchUnit.LinkTo = GetBatchUnit.Key;
                        Units.Add(NextBatchUnit);
                    }

                    return;
                }
            }

            UnitGroup BatchUnit = new UnitGroup(this);
            BatchUnit.Init(GenKey, Item, AggregationMode.Aggregation);
            Units.Add(BatchUnit);
        }

        public int GetUnitsCount()
        {
            int Count = 0;
            for (int i = 0; i < this.Units.Count; i++)
            {
                Count += this.Units[i].GetCount();
            }
            return Count;
        }

        public int GetCount()
        {
            if (UnionData != null)
            {
                return UnionData.GetCount();
            }
            else
            {
                return (GetUnitsCount() + this.SameItems.Count + this.Books.Count);
            }
        }
        public static ProcContent Build(Translator Translator, UnionArray Data,AggregationMode SetMode)
        {
            ProcContent Content = new ProcContent(Translator);
            Content.UnionData = Data;

            if (SetMode == AggregationMode.Aggregation)
            {
                List<UnitGroup> SameItems = new List<UnitGroup>();
                List<BaseUnit> UniqueLeaders = new List<BaseUnit>();

                HashSet<string> SeenTexts = new HashSet<string>();

                foreach (var Leader in Data.Leaders.Values)
                {
                    if (!SeenTexts.Contains(Leader.Original))
                    {
                        SeenTexts.Add(Leader.Original);
                        UniqueLeaders.Add(Leader);
                    }
                    else
                    {
                        SameItems.Add(new UnitGroup(Content, Leader));
                    }
                }

                foreach (var Leader in UniqueLeaders)
                {
                    Content.AddLeader(Leader);
                }

                foreach (var Unit in Data.Units)
                {
                    if (!SeenTexts.Contains(Unit.Original))
                    {
                        SeenTexts.Add(Unit.Original);
                        Content.Add(Unit);
                    }
                    else
                    {
                        SameItems.Add(new UnitGroup(Content, Unit));
                    }
                }

                Dictionary<UnitGroup, BaseUnit> SingleUnits = new Dictionary<UnitGroup, BaseUnit>();
                for (int i = 0; i < Content.Units.Count; i++)
                {
                    if (Content.Units[i].Units.Count == 1)
                    {
                        SingleUnits.Add(Content.Units[i], Content.Units[i].Units[0]);
                    }
                }

                UnitGroup UnitGroup = new UnitGroup(Content);

                foreach (var Kvp in SingleUnits)
                {
                    Content.Units.Remove(Kvp.Key);
                    UnitGroup.AddUnit(Kvp.Value);

                    if (UnitGroup.TotalLength > ProcContent.TextLengthLimit)
                    {
                        Content.Units.Add(UnitGroup);
                        UnitGroup = new UnitGroup(Content);
                        UnitGroup.IsUnrelated = true;
                    }
                }

                if (UnitGroup.Units.Count > 0)
                {
                    Content.Units.Add(UnitGroup);
                }

                Content.SameItems.AddRange(SameItems);
            }
            else
            if (SetMode == AggregationMode.Single)
            {
              
            }

            return Content;
        }
    }
}
