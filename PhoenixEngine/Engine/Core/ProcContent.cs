using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using PhoenixEngine.Game;
using PhoenixEngine.Language;
using PhoenixEngine.Sequence;
using PhoenixEngine.Translate;
using PhoenixEngine.Unit;

namespace PhoenixEngine.Engine
{

    //I have been considering the issue of bucketing. Based on user feedback, it is undoubtedly very logical to bucket files according to the interdependencies of the ESP files themselves, and I have implemented this association functionality in EspReader.
    //The main issue is that I want PhoenixEngine to serve not just as a file translator for Skyrim, but as a universal translation core.
    //Grouping by similarity supports all file types—including JSON, ESP, and PEX—and effectively ensures translation consistency. In contrast, grouping by association is limited to ESP files—and specifically to certain types of ESP files—resulting in limited applicability.

    //I need to come up with a good way to make both of them compatible.
    internal class P_BucketContainer
    {
        public HashSet<string> AddedKeys = new HashSet<string>();

        public int BucketLengthLimit = 3000;

        public List<P_Bucket> Buckets = new List<P_Bucket>();

        //From a purely user-centric perspective, I believe that grouping by relevance is the right approach. When considering the inherent connections within the game itself, there is no doubt that bucketing items in the standard sequence yields higher-quality translations.
        //From a developer's perspective, grouping by similarity makes sense. It is a universal and stable approach; it remains unaffected by changes in file types, so any resulting side effects are negligible.

        //So, I plan to implement a function pointer that the calling program uses during the bucketing process to determine whether the current `BaseUnit` has any associations. If there are no associations, it returns `null`, and the system proceeds with its standard similarity-based grouping; if there are associations, it performs bucketing based on the returned array. This offers the best of both worlds.
        public delegate List<BaseUnit> CalculateSimilarity(BaseUnit Unit);
        public CalculateSimilarity CalculateSimilarityEvent = null;


        //foreach(var GetBaseUnit in BaseUnits)
        //Don't forget that we converted all the `Record` objects into a `List<BaseUnit>`, so we need to iterate through them.
        //However, if we query for associations during this iteration, we might end up adding the same items repeatedly.Therefore, we should construct a `HashKey` to track the primary keys of items already added, ensuring that each entry is included only once.
        public bool CheckKey(string Key)
        {
            if (AddedKeys.Contains(Key))
            {
                return true;
            }

            return false;
        }

        public static int CalcTokenLength(string Text, Languages From)
        {
            if (string.IsNullOrEmpty(Text))
                return 0;

            int RawLength = Text.Length;

            switch (From)
            {
                case Languages.SimplifiedChinese:
                case Languages.TraditionalChinese:
                case Languages.Japanese:
                case Languages.Korean:
                    return (int)(RawLength * 2.5);

                case Languages.Thai:
                case Languages.Hindi:
                case Languages.Urdu:
                case Languages.Persian:
                case Languages.Russian:
                case Languages.Ukrainian:
                    return (int)(RawLength * 1.5);

                default:
                    return RawLength;
            }
        }

        public void Add(BaseUnit Item)
        {
            if (!CheckKey(Item.Key))
            {
                var LinkItems = CalculateSimilarityEvent?.Invoke(Item);

                //There is another issue here: we need to convert the "buckets" into actual requests to the AI, but the target platform imposes token limits. This means we must account for scenarios where the text volume exceeds these limits, which would result in the buckets being truncated.
                //If I recall correctly, it is 4,096 bytes; to be conservative, I am using a bucket size of 3,000 bytes. I also need to account for the fact that English characters take up one byte, whereas Japanese and Chinese characters actually occupy multiple bytes, even though the old byte-calculation method is still being used.

                int TotalSize = 0;
                if (LinkItems != null)
                {
                    foreach (var GetItem in LinkItems)
                    {
                        TotalSize += CalcTokenLength(GetItem.Original,P_Language.DetectLanguageByLine(GetItem.Original));
                    }
                }

                bool IsAdded = false;

                //Check for available empty containers.
                for (int i = 0; i < this.Buckets.Count; i++)
                {
                    if (this.Buckets[i].RemainingSize >= TotalSize)
                    {
                        this.Buckets[i].BaseUnits.AddRange(LinkItems);
                        IsAdded = true;
                        break;
                    }
                }
                //Try to fill all the buckets as much as possible to reduce the number of requests.
                //If there is no bucket that can accommodate it, create a new one.
                if (!IsAdded)
                {
                    this.Buckets.Add(new P_Bucket(this.BucketLengthLimit));
                    this.Buckets[this.Buckets.Count - 1].BaseUnits.AddRange(LinkItems);//Insert into a new bucket
                }
                
                AddedKeys.Add(Item.Key);
            }
        }
    }
    internal class P_Bucket
    {
        public int RemainingSize = 0;
        public int ID = 0;
        public List<BaseUnit> BaseUnits = new List<BaseUnit>();
        public int Next = 0;

        public P_Bucket(int RemainingSize)
        { 
           this.RemainingSize = RemainingSize;
        }
    }

    internal class AggregatedTranslation
    {
        public List<P_Bucket> Buckets = new List<P_Bucket>();
        public List<BaseUnit> Books = new List<BaseUnit>();
    }
    
    public class ProcContent
    {
        public static int TokenLengthLimit = 2000;

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

        public static int CalcTokenLength(string Text, Languages From)
        {
            if (string.IsNullOrEmpty(Text))
                return 0;

            int RawLength = Text.Length;

            switch (From)
            {
                case Languages.SimplifiedChinese:
                case Languages.TraditionalChinese:
                case Languages.Japanese:
                case Languages.Korean:
                    return (int)(RawLength * 2.5);

                case Languages.Thai:
                case Languages.Hindi:
                case Languages.Urdu:
                case Languages.Persian:
                case Languages.Russian:
                case Languages.Ukrainian:
                    return (int)(RawLength * 1.5);

                default:
                    return RawLength;
            }
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
            this.Units?.Clear();
            this.SameItems?.Clear();
            this.Books?.Clear();
            this.GenKey = 0;
            this.UnionData?.Clear();
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

        private static UnitGroup MakeSoloBucket(ref int GenKey, BaseUnit Unit, int TokenLen)
        {
            GenKey++;
            UnitGroup Solo = new UnitGroup();
            Solo.Key = GenKey.ToString();
            Solo.Mode = AggregationMode.Aggregation;
            Solo.AnchorTokens = new HashSet<string>();
            Solo.AllTokens = new HashSet<string>();
            Solo.AddUnit(Unit, TokenLen);
            return Solo;
        }

        public static ProcContent Build(Translator Translator, UnionArray Data, AggregationMode SetMode)
        {
            ProcContent Content = new ProcContent(Translator);
            Content.UnionData = Data;

            Languages From = Translator.From;

            if (SetMode == AggregationMode.Aggregation)
            {
                List<UnitGroup> SameItems = new List<UnitGroup>();
                HashSet<string> SeenTexts = new HashSet<string>();

                Dictionary<string, UnitGroup> LeaderKeyMap = new Dictionary<string, UnitGroup>();

                foreach (var Leader in Data.Leaders.Values)
                {
                    P_Game GameType = P_Game.Null;
                    if (P_Skyrim.IsBookContent(Leader, ref GameType))
                    {
                        Content.Books.Add(new UnitGroup(Translator,Leader));
                        continue;
                    }

                    if (SeenTexts.Contains(Leader.Original))
                    {
                        SameItems.Add(new UnitGroup(Translator,Leader));
                        continue;
                    }

                    SeenTexts.Add(Leader.Original);
                    Content.GenKey++;

                    int LeaderTokenLen = CalcTokenLength(Leader.Original, From);

                    UnitGroup Bucket = new UnitGroup();
                    Bucket.Key = Leader.Key;
                    Bucket.Mode = AggregationMode.Aggregation;
                    Bucket.AnchorTokens = Leader.ExtractTokens(Translator);
                    Bucket.AllTokens = new HashSet<string>(Bucket.AnchorTokens);
                    Bucket.AddUnit(Leader, LeaderTokenLen);

                    Content.Units.Add(Bucket);
                    LeaderKeyMap[Leader.Key] = Bucket;
                }

                Queue<BaseUnit> RemainingUnits = new Queue<BaseUnit>();

                foreach (var Unit in Data.Units)
                {
                    P_Game GameType = P_Game.Null;
                    if (P_Skyrim.IsBookContent(Unit, ref GameType))
                    {
                        Content.Books.Add(new UnitGroup(Translator,Unit));
                        continue;
                    }

                    if (SeenTexts.Contains(Unit.Original))
                    {
                        SameItems.Add(new UnitGroup(Translator,Unit));
                        continue;
                    }

                    SeenTexts.Add(Unit.Original);

                    var UnitTokens = Unit.ExtractTokens(Translator);
                    bool Assigned = false;

                    int UnitTokenLen = CalcTokenLength(Unit.Original, From);

                    foreach (var Leader in Data.Leaders.Values)
                    {
                        if (!LeaderKeyMap.TryGetValue(Leader.Key, out UnitGroup ActiveBucket))
                            continue;

                        if (!ActiveBucket.IsSimilarTo(UnitTokens, 1))
                            continue;

                        if (ActiveBucket.TokenLength + UnitTokenLen < TokenLengthLimit)
                        {
                            ActiveBucket.AddUnit(Unit, UnitTokens, UnitTokenLen);
                        }
                        else
                        {
                            Content.GenKey++;

                            UnitGroup OverflowBucket = new UnitGroup();
                            OverflowBucket.Key = Content.GenKey.ToString();
                            OverflowBucket.Mode = AggregationMode.Aggregation;
                            OverflowBucket.AnchorTokens = new HashSet<string>(ActiveBucket.AnchorTokens);
                            OverflowBucket.AllTokens = new HashSet<string>(ActiveBucket.AnchorTokens);
                            OverflowBucket.LinkTo = ActiveBucket;
                            OverflowBucket.AddUnit(Unit, UnitTokens, UnitTokenLen);

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

                    int FirstTokenLen = CalcTokenLength(GetFirst.Original, From);

                    if (FirstTokenLen >= TokenLengthLimit)
                    {
                        Content.Units.Add(MakeSoloBucket(ref Content.GenKey, GetFirst, FirstTokenLen));
                        continue;
                    }

                    int BestIndex = -1;
                    int MaxRemaining = -1;

                    for (int i = 0; i < Content.Units.Count; i++)
                    {
                        var Group = Content.Units[i];

                        int Remaining = TokenLengthLimit - Group.TokenLength - FirstTokenLen;

                        if (Remaining < 0)
                            continue;

                        if (Remaining > MaxRemaining)
                        {
                            MaxRemaining = Remaining;
                            BestIndex = i;
                        }
                    }

                    if (BestIndex != -1)
                        Content.Units[BestIndex].AddUnit(GetFirst, FirstTokenLen);
                    else
                        StillRemaining.Enqueue(GetFirst);
                }

                while (StillRemaining.Count > 0)
                {
                    BaseUnit Head = StillRemaining.Peek();

                    int HeadTokenLen = CalcTokenLength(Head.Original, From);

                    if (HeadTokenLen >= TokenLengthLimit)
                    {
                        StillRemaining.Dequeue();
                        Content.Units.Add(MakeSoloBucket(ref Content.GenKey, Head, HeadTokenLen));
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
                        int PeekTokenLen = CalcTokenLength(Peek.Original, From);

                        if (NewBucket.TokenLength + PeekTokenLen < TokenLengthLimit)
                        {
                            StillRemaining.Dequeue();
                            NewBucket.AddUnit(Peek, PeekTokenLen);
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (NewBucket.Units.Count > 0)
                        Content.Units.Add(NewBucket);
                }

                var Sorted = Content.Units.OrderByDescending(u => u.TokenLength).ToList();
                var Merged = new List<UnitGroup>();

                foreach (var Unit in Sorted)
                {
                    bool Placed = false;
                    foreach (var Bucket in Merged)
                    {
                        if (Bucket.TokenLength + Unit.TokenLength < TokenLengthLimit)
                        {
                            foreach (var Token in Unit.AllTokens)
                                Bucket.AllTokens.Add(Token);
                            foreach (var U in Unit.Units)
                                Bucket.AddUnit(U, CalcTokenLength(U.Original, From));
                            Placed = true;
                            break;
                        }
                    }

                    if (!Placed)
                        Merged.Add(Unit);
                }

                Content.Units = Merged;

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

            return Content;
        }

        public static void ArrangeForParallel(ProcContent Content, int ThreadCount)
        {
            List<UnitGroup> Source = new List<UnitGroup>(Content.Units);
            List<UnitGroup> Result = new List<UnitGroup>();

            Dictionary<UnitGroup, int> BatchIndexMap = new Dictionary<UnitGroup, int>();

            Queue<UnitGroup> Pending = new Queue<UnitGroup>(Source);

            int BatchIndex = 0;

            while (Pending.Count > 0)
            {
                List<UnitGroup> Slot = new List<UnitGroup>();
                List<UnitGroup> Deferred = new List<UnitGroup>();

                foreach (var Group in Pending)
                {
                    if (Slot.Count >= ThreadCount)
                    {
                        Deferred.Add(Group);
                        continue;
                    }

                    if (Group.LinkTo != null)
                    {
                        if (!BatchIndexMap.TryGetValue(Group.LinkTo, out int LeaderBatch))
                        {
                            Deferred.Add(Group);
                            continue;
                        }

                        if (LeaderBatch >= BatchIndex)
                        {
                            Deferred.Add(Group);
                            continue;
                        }
                    }

                    Slot.Add(Group);
                    BatchIndexMap[Group] = BatchIndex;
                }

                if (Slot.Count == 0 && Deferred.Count > 0)
                {
                    var Force = Deferred[0];
                    Deferred.RemoveAt(0);
                    Slot.Add(Force);
                    BatchIndexMap[Force] = BatchIndex;
                }

                Result.AddRange(Slot);
                Pending = new Queue<UnitGroup>(Deferred);
                BatchIndex++;
            }

            Content.Units = Result;
        }
    }
}