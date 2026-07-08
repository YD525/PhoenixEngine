using System.Collections.Generic;
using System.Linq;
using System.Web.UI.WebControls;
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
        public HashSet<string> Heads = new HashSet<string>();

        public int BucketLengthLimit = 3000;

        public UnionArray Data = null;

        public List<BaseUnit> Units = new List<BaseUnit>();
        public List<P_Bucket> Buckets = new List<P_Bucket>();

        //From a purely user-centric perspective, I believe that grouping by relevance is the right approach. When considering the inherent connections within the game itself, there is no doubt that bucketing items in the standard sequence yields higher-quality translations.
        //From a developer's perspective, grouping by similarity makes sense. It is a universal and stable approach; it remains unaffected by changes in file types, so any resulting side effects are negligible.

        //So, I plan to implement a function pointer that the calling program uses during the bucketing process to determine whether the current `BaseUnit` has any associations. If there are no associations, it returns `null`, and the system proceeds with its standard similarity-based grouping; if there are associations, it performs bucketing based on the returned array. This offers the best of both worlds.
        public delegate List<BaseUnit> CalculateSimilarity(BaseUnit Unit);
        public CalculateSimilarity CalculateSimilarityEvent = null;

        public P_BucketContainer(List<BaseUnit> BaseUnits)
        {
            this.Units = BaseUnits;
            ChooseHeads();
        }

        //public void MarkLeadersAndSort(List<BaseUnit> SetBaseUnits, Languages Lang, ref double MarkLeadersPercent)
        //{
        //    MarkLeadersPercent = 0;

        //    int N = SetBaseUnits.Count;
        //    if (N == 0)
        //        return;

        //    Leaders.Clear();
        //    Units.Clear();

        //    int MaxCharsForLeaderSelection = Phoenix.Config.ContextLimit;

        //    var FilteredItems = new List<int>();

        //    for (int i = 0; i < N; i++)
        //    {
        //        var Item = SetBaseUnits[i];
        //        Item.TempSim = 0;

        //        if (!string.IsNullOrEmpty(Item.Original) &&
        //            Item.Original.Length > MaxCharsForLeaderSelection)
        //        {
        //            Units.Add(Item);
        //        }
        //        else
        //        {
        //            FilteredItems.Add(i);
        //        }
        //    }

        //    if (FilteredItems.Count == 0)
        //    {
        //        MarkLeadersPercent = 100;
        //        return;
        //    }

        //    var TokensCache = new Dictionary<int, HashSet<string>>(FilteredItems.Count);

        //    foreach (var Item in FilteredItems)
        //    {
        //        var Token = TextTokenizer.BuildTokenSignature(Lang, SetBaseUnits[Item].Original);
        //        TokensCache[Item] = Token.Take(10).ToHashSet();
        //    }

        //    var PrefixBuckets = new Dictionary<string, List<int>>();

        //    foreach (var Item in FilteredItems)
        //    {
        //        var Prefix = ContextProc.BuildPrefixKey(SetBaseUnits[Item].Original, 3);

        //        if (!PrefixBuckets.TryGetValue(Prefix, out var List))
        //        {
        //            List = new List<int>();
        //            PrefixBuckets[Prefix] = List;
        //        }

        //        List.Add(Item);
        //    }

        //    int ProcessedCount = Units.Count;
        //    int TotalToProcess = N;
        //    int UpdateInterval = Math.Max(1, TotalToProcess / 100);

        //    foreach (var Bucket in PrefixBuckets.Values)
        //    {
        //        if (Bucket.Count == 0)
        //            continue;

        //        if (Bucket.Count == 1)
        //        {
        //            Units.Add(SetBaseUnits[Bucket[0]]);
        //            ProcessedCount++;
        //            continue;
        //        }

        //        int LeaderIndex = ContextProc.PickContextLeader(Bucket, SetBaseUnits, TokensCache);
        //        var LeaderItem = SetBaseUnits[LeaderIndex];

        //        LeaderItem.TempSim = Bucket.Count - 1;

        //        if (!string.IsNullOrEmpty(LeaderItem.Key))
        //        {
        //            LeaderItem.Leader = true;
        //            Leaders[LeaderItem.Key] = LeaderItem;
        //        }
        //        else
        //        {
        //            Units.Add(LeaderItem);
        //        }

        //        ProcessedCount++;

        //        foreach (var Item in Bucket)
        //        {
        //            if (Item == LeaderIndex)
        //                continue;

        //            Units.Add(SetBaseUnits[Item]);
        //            ProcessedCount++;
        //        }

        //        if (ProcessedCount % UpdateInterval == 0)
        //        {
        //            MarkLeadersPercent = Math.Round(Math.Min(ProcessedCount, TotalToProcess) * 100.0 / TotalToProcess, 2);
        //        }
        //    }

        //    var SecondStageMap = new Dictionary<string, int>();
        //    var RemoveLeaders = new List<string>();

        //    foreach (var KV in Leaders)
        //    {
        //        var Item = KV.Value;
        //        var Key2 = ContextProc.BuildPrefixKey(Item.Original, 2);

        //        if (SecondStageMap.ContainsKey(Key2))
        //        {
        //            Units.Add(Item);
        //            RemoveLeaders.Add(KV.Key);
        //        }
        //        else
        //        {
        //            SecondStageMap[Key2] = 1;
        //        }
        //    }

        //    foreach (var K in RemoveLeaders)
        //    {
        //        Leaders.Remove(K);
        //    }

        //    MarkLeadersPercent = 100;
        //}

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

        public void ChooseHeads()
        {
            //The key point here is that "Head" is a standalone node; after N-gram tokenization, it serves as a marker that can be placed into a bucket.
            //However, when relevance logic is incorporated, if a "Head" happens to have an association, it can no longer serve as the "Head" and must be removed; otherwise, irrelevant content would be included.
            //Two logic components—one handling similarity and the other handling association—need to be compatible; the key lies in their execution order.
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
                        TotalSize += CalcTokenLength(GetItem.Original, P_Language.DetectLanguageByLine(GetItem.Original));
                    }
                }
                else
                {
                    //Since we group items based on similarity, calculating similarity relative to the previous item would lead to significant "drift" in the bucket's contents—because D is linked to C, and C is linked to B. This results in poor overall similarity within the bucket. We originally adopted the "Leader" mechanism, and we continue to use this approach today.
                    //I plan to redo the similarity component; originally, I had AI write part of it out of laziness, which meant I failed to check many sections. A user also pointed out that content was being skipped during translation—leaving a third of the material untranslated. While I initially considered simply running another scan, that would only address the symptoms rather than the root cause. So now, I am implementing it entirely on my own.
               
                    //
                }

                bool IsAdded = false;

                //Check for any buckets that are not fully filled.
                for (int i = 0; i < this.Buckets.Count; i++)
                {
                    if (this.Buckets[i].RemainingSize >= TotalSize)
                    {
                        this.Buckets[i].Add(LinkItems,TotalSize);
                        IsAdded = true;
                        break;
                    }
                }

                //Try to fill all the buckets as much as possible to reduce the number of requests.
                //If there is no bucket that can accommodate it, create a new one.
                if (!IsAdded)
                {
                    this.Buckets.Add(new P_Bucket(this.AddedKeys, null,this.BucketLengthLimit));
                    this.Buckets[this.Buckets.Count - 1].Add(LinkItems,TotalSize);//Insert into a new bucket
                }

                //Actually, there is still an issue with this approach: even if we group items based on relevance, unrelated content might still end up in the same bucket during the bucketing process.
                //Therefore, the leader for the group based on association must be null.
                //We need to pack unrelated texts into these buckets; although this increases the number of requests—potentially resulting in many partially filled buckets—it improves quality.
                AddedKeys.Add(Item.Key);
            }
        }
    }
    internal class P_Bucket
    {
        public int RemainingSize = 0;
        public int ID = 0;
        private BaseUnit Head = null;//"Leader" doesn't sound great; "Head" would be better.
        private List<BaseUnit> BaseUnits = new List<BaseUnit>();
        public int Next = 0;
        public HashSet<string> KeysRef;
        public P_Bucket(HashSet<string>KeysRef,BaseUnit Head, int RemainingSize)
        { 
           this.Head = Head;
           this.RemainingSize = RemainingSize;
           this.BaseUnits.Add(this.Head);

           this.KeysRef = KeysRef;

           if (!this.KeysRef.Contains(Head.Key))
           {
               this.KeysRef.Add(Head.Key);
           }
        }

        public void Add(List<BaseUnit> Units, int Size)
        {
            this.RemainingSize -= Size;

            foreach (var GetUnit in Units)
            {
                if (!this.KeysRef.Contains(GetUnit.Key))
                {
                    this.KeysRef.Add(GetUnit.Key);
                }
                else
                {
                    throw new System.Exception();
                }
            }

            this.BaseUnits.AddRange(Units);
        }

        public void Add(BaseUnit Unit, int Size)
        {
            this.RemainingSize -= Size;

            if (!this.KeysRef.Contains(Unit.Key))
            {
                this.KeysRef.Add(Unit.Key);
            }
            else
            {
                throw new System.Exception();
            }

            this.BaseUnits.Add(Unit);
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