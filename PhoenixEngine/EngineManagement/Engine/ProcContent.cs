using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
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
        public List<UnitGroup> Books = new List<UnitGroup>();
        public List<UnitGroup> SameItems = new List<UnitGroup>();

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

            UnitGroup BatchUnit = new UnitGroup();
            BatchUnit.Init(GenKey, Item, AggregationMode.Aggregation);
            Units.Add(BatchUnit);
        }
        private bool TryAdd(BaseUnit Item)
        {
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
                        GenKey++;

                        UnitGroup NextBatchUnit = new UnitGroup();

                        NextBatchUnit.Key = GenKey.ToString();

                        NextBatchUnit.AnchorTokens = new HashSet<string>(GetBatchUnit.AnchorTokens);
                        NextBatchUnit.AllTokens = new HashSet<string>(NextBatchUnit.AnchorTokens);

                        NextBatchUnit.AddUnit(Item, GenTokens);

                        NextBatchUnit.LinkTo = GetBatchUnit.Key;
                        Units.Add(NextBatchUnit);
                    }

                    return true;
                }
            }


            return false;
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

        public void Clear()
        {
            this.UnionData.Clear();

            this.Units.Clear();
            this.SameItems.Clear();
            this.Books.Clear();
        }

        private void BuildTranslatedMap(List<UnitGroup> Groups, Dictionary<string, string> TranslatedMap)
        {
            foreach (var Group in Groups)
            {
                foreach (var Unit in Group.Units)
                {
                    if (!string.IsNullOrEmpty(Unit.Original)
                        && !string.IsNullOrEmpty(Unit.Translated))
                    {
                        TranslatedMap[Unit.Original] = Unit.Translated;
                    }
                }
            }
        }
        public void SyncSameItemsFromTranslated()
        {
            Dictionary<string, string> TranslatedMap = new Dictionary<string, string>();

            BuildTranslatedMap(this.Units, TranslatedMap);
            BuildTranslatedMap(this.Books, TranslatedMap);

            foreach (var Group in SameItems)
            {
                foreach (var Unit in Group.Units)
                {
                    if (TranslatedMap.TryGetValue(Unit.Original, out var Translated))
                    {
                        Unit.Translated = Translated;
                    }
                }
            }
        }
        public static ProcContent Build(Translator Translator, UnionArray Data, AggregationMode SetMode)
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
                    Game GameType = Game.Null;

                    if (SkyrimBookHelper.IsSkyrimBook(Leader, ref GameType))
                    {
                        Content.Books.Add(new UnitGroup(Leader));
                        continue;
                    }

                    if (!SeenTexts.Contains(Leader.Original))
                    {
                        SeenTexts.Add(Leader.Original);
                        Content.AddLeader(Leader);
                    }
                    else
                    {
                        SameItems.Add(new UnitGroup(Leader));
                    }
                }
                Queue<BaseUnit> RemainingUnits = new Queue<BaseUnit>();

                foreach (var Unit in Data.Units)
                {
                    Game GameType = Game.Null;
                    if (SkyrimBookHelper.IsSkyrimBook(Unit, ref GameType))
                    {
                        Content.Books.Add(new UnitGroup(Unit));
                        continue;
                    }

                    if (!SeenTexts.Contains(Unit.Original))
                    {
                        SeenTexts.Add(Unit.Original);

                        if (!Content.TryAdd(Unit))
                        {
                            RemainingUnits.Enqueue(Unit);
                        }
                    }
                    else
                    {
                        SameItems.Add(new UnitGroup(Unit));
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

                foreach (var Kvp in SingleUnits)
                {
                    Content.Units.Remove(Kvp.Key);
                }


                foreach (var Kvp in SingleUnits)
                {
                    RemainingUnits.Enqueue(Kvp.Value);
                }

                while (RemainingUnits.Count > 0)
                {
                    BaseUnit GetFirst = RemainingUnits.Dequeue();

                    int BestIndex = -1;
                    int MinUnitCount = int.MaxValue;

                    for (int i = 0; i < Content.Units.Count; i++)
                    {
                        var Group = Content.Units[i];

                        if (Group.TotalLength + GetFirst.Original.Length >= ProcContent.TextLengthLimit)
                            continue;

                        int Count = Group.Units.Count;

                        if (Count < MinUnitCount)
                        {
                            MinUnitCount = Count;
                            BestIndex = i;
                        }
                    }

                    if (BestIndex == -1)
                        break;

                    Content.Units[BestIndex].AddUnit(GetFirst);
                }

                UnitGroup NextBatchUnit = new UnitGroup();

                while (RemainingUnits.Count > 0)
                {
                    Content.GenKey++;

                    NextAdd:

                    if (RemainingUnits.Count == 0)
                    {
                        break;
                    }

                    BaseUnit GetFrist = RemainingUnits.Dequeue();

                    NextBatchUnit.Key = Content.GenKey.ToString();

                    NextBatchUnit.AnchorTokens = new HashSet<string>();
                    NextBatchUnit.AllTokens = new HashSet<string>();

                    if ((NextBatchUnit.TotalLength + GetFrist.Original.Length) < ProcContent.TextLengthLimit)
                    {
                        NextBatchUnit.AddUnit(GetFrist);
                        goto NextAdd;
                    }
                    else
                    {
                        NextBatchUnit.LinkTo = "";
                        Content.Units.Add(NextBatchUnit);

                        NextBatchUnit = new UnitGroup();
                        NextBatchUnit.AddUnit(GetFrist);
                    }
                }

                if (NextBatchUnit.Units.Count > 0)
                {
                    Content.Units.Add(NextBatchUnit);
                    NextBatchUnit = null;
                }

                Content.SameItems.AddRange(SameItems);
            }
            else
            if (SetMode == AggregationMode.Single)
            {
                foreach (var GetUnit in Data.Leaders)
                {
                    Content.Units.Add(Translator.ToUnitGroup(GetUnit.Value));
                }
                foreach (var GetUnit in Data.Units)
                {
                    Content.Units.Add(Translator.ToUnitGroup(GetUnit));
                }
            }

            //string GetJson = JsonConvert.SerializeObject(Content, Formatting.Indented);
            return Content;
        }
    }
}
