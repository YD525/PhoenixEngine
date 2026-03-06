using System.Collections.Generic;
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
            this.Units.Clear();
            this.SameItems.Clear();
            this.Books.Clear();

            this.GenKey = 0;
            this.UnionData.Clear();
        }

        private void BuildTranslatedMap(List<UnitGroup> Groups, Dictionary<string, string> TranslatedMap)
        {
            foreach (var Group in Groups)
            {
                foreach (var Unit in Group.Units)
                {
                    if (!string.IsNullOrEmpty(Unit.Translated))
                    {
                        string RealOriginal = Unit.GetRealOriginal();
                        if (!string.IsNullOrEmpty(RealOriginal))
                            TranslatedMap[RealOriginal] = Unit.Translated;

                        if (!string.IsNullOrEmpty(Unit.Original) && Unit.Original != RealOriginal)
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
                    string RealOriginal = Unit.GetRealOriginal();
                    if (!string.IsNullOrEmpty(RealOriginal) && TranslatedMap.TryGetValue(RealOriginal, out var Trans1))
                    {
                        Unit.Translated = Trans1;
                    }
                    else if (!string.IsNullOrEmpty(Unit.Original) && TranslatedMap.TryGetValue(Unit.Original, out var Trans2))
                    {
                        Unit.Translated = Trans2;
                    }
                }
            }
        }

        private static UnitGroup MakeSoloBucket(ref int GenKey, BaseUnit Unit)
        {
            GenKey++;
            UnitGroup Solo = new UnitGroup();
            Solo.Key = GenKey.ToString();
            Solo.Mode = AggregationMode.Aggregation;
            Solo.AnchorTokens = new HashSet<string>();
            Solo.AllTokens = new HashSet<string>();
            Solo.AddUnit(Unit);
            return Solo;
        }

        public static ProcContent Build(Translator Translator, UnionArray Data, AggregationMode SetMode)
        {
            ProcContent Content = new ProcContent(Translator);
            Content.UnionData = Data;

            if (SetMode == AggregationMode.Aggregation)
            {
                List<UnitGroup> SameItems = new List<UnitGroup>();
                HashSet<string> SeenTexts = new HashSet<string>();

                Dictionary<string, UnitGroup> LeaderKeyMap = new Dictionary<string, UnitGroup>();

                foreach (var Leader in Data.Leaders.Values)
                {
                    Game GameType = Game.Null;
                    if (SkyrimBookHelper.IsSkyrimBook(Leader, ref GameType))
                    {
                        Content.Books.Add(new UnitGroup(Leader));
                        continue;
                    }

                    if (SeenTexts.Contains(Leader.Original))
                    {
                        SameItems.Add(new UnitGroup(Leader));
                        continue;
                    }

                    SeenTexts.Add(Leader.Original);
                    Content.GenKey++;

                    UnitGroup Bucket = new UnitGroup();
                    Bucket.Key = Leader.Key;
                    Bucket.Mode = AggregationMode.Aggregation;
                    Bucket.AnchorTokens = Leader.ExtractTokens();
                    Bucket.AllTokens = new HashSet<string>(Bucket.AnchorTokens);
                    Bucket.AddUnit(Leader);

                    Content.Units.Add(Bucket);
                    LeaderKeyMap[Leader.Key] = Bucket;
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

                    if (SeenTexts.Contains(Unit.Original))
                    {
                        SameItems.Add(new UnitGroup(Unit));
                        continue;
                    }

                    SeenTexts.Add(Unit.Original);

                    var UnitTokens = Unit.ExtractTokens();
                    bool Assigned = false;

                    foreach (var Leader in Data.Leaders.Values)
                    {
                        if (!LeaderKeyMap.TryGetValue(Leader.Key, out UnitGroup ActiveBucket))
                            continue;

                        if (!ActiveBucket.IsSimilarTo(UnitTokens, 1))
                            continue;

                        if (ActiveBucket.TotalLength + Unit.Original.Length < TextLengthLimit)
                        {
                            ActiveBucket.AddUnit(Unit, UnitTokens);
                        }
                        else
                        {
                            Content.GenKey++;

                            UnitGroup OverflowBucket = new UnitGroup();
                            OverflowBucket.Key = Content.GenKey.ToString();
                            OverflowBucket.Mode = AggregationMode.Aggregation;
                            OverflowBucket.AnchorTokens = new HashSet<string>(ActiveBucket.AnchorTokens);
                            OverflowBucket.AllTokens = new HashSet<string>(ActiveBucket.AnchorTokens);
                            OverflowBucket.LinkTo = ActiveBucket.Key;
                            OverflowBucket.AddUnit(Unit, UnitTokens);

                            Content.Units.Add(OverflowBucket);
                            LeaderKeyMap[Leader.Key] = OverflowBucket;
                        }

                        Assigned = true;
                        break; 
                    }

                    if (!Assigned)
                        RemainingUnits.Enqueue(Unit);
                }

                Queue<BaseUnit> StillRemaining = new Queue<BaseUnit>();

                while (RemainingUnits.Count > 0)
                {
                    BaseUnit GetFirst = RemainingUnits.Dequeue();

                    if (GetFirst.Original.Length >= TextLengthLimit)
                    {
                        Content.Units.Add(MakeSoloBucket(ref Content.GenKey, GetFirst));
                        continue;
                    }

                    int BestIndex = -1;
                    int MinUnitCount = int.MaxValue;

                    for (int i = 0; i < Content.Units.Count; i++)
                    {
                        var Group = Content.Units[i];

                        if (Group.TotalLength + GetFirst.Original.Length >= TextLengthLimit)
                            continue;

                        if (Group.Units.Count < MinUnitCount)
                        {
                            MinUnitCount = Group.Units.Count;
                            BestIndex = i;
                        }
                    }

                    if (BestIndex != -1)
                        Content.Units[BestIndex].AddUnit(GetFirst);
                    else
                        StillRemaining.Enqueue(GetFirst);
                }

                while (StillRemaining.Count > 0)
                {
                    BaseUnit Head = StillRemaining.Peek();

                    if (Head.Original.Length >= TextLengthLimit)
                    {
                        StillRemaining.Dequeue();
                        Content.Units.Add(MakeSoloBucket(ref Content.GenKey, Head));
                        continue;
                    }

                    Content.GenKey++;
                    UnitGroup NewBucket = new UnitGroup();
                    NewBucket.Key = Content.GenKey.ToString();
                    NewBucket.Mode = AggregationMode.Aggregation;
                    NewBucket.AnchorTokens = new HashSet<string>();
                    NewBucket.AllTokens = new HashSet<string>();

                    while (StillRemaining.Count > 0)
                    {
                        BaseUnit Peek = StillRemaining.Peek();

                        if (NewBucket.TotalLength + Peek.Original.Length < TextLengthLimit)
                        {
                            StillRemaining.Dequeue();
                            NewBucket.AddUnit(Peek);
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (NewBucket.Units.Count > 0)
                        Content.Units.Add(NewBucket);
                }

                Content.SameItems.AddRange(SameItems);
            }
            else if (SetMode == AggregationMode.Single)
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

            string GetJson = JsonConvert.SerializeObject(Content, Formatting.Indented);
            return Content;
        }
    }
}