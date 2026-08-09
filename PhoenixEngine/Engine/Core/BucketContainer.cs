using System;
using System.Collections.Generic;
using System.Linq;
using PhoenixEngine.Engine.Core;
using PhoenixEngine.Translate;
using PhoenixEngine.Unit;

namespace PhoenixEngine.Engine
{
    public class P_BucketContainer
    {
        public HashSet<string> AddedKeys = new HashSet<string>();


        public Dictionary<string, BaseUnit> Heads = new Dictionary<string, BaseUnit>();

        public List<BaseUnit> TempUnits = new List<BaseUnit>();
        public List<P_Bucket> UnitBuckets = new List<P_Bucket>();
        public List<P_Bucket> BookBuckets = new List<P_Bucket>();

        public List<UnitGroup> Units = new List<UnitGroup>();
        public List<UnitGroup> Books = new List<UnitGroup>();

        public delegate List<BaseUnit> CheckLinks(List<BaseUnit> TempUnits, BaseUnit Unit);
        public CheckLinks CheckLinksEvent = null;

        public void Clear()
        {
            TempUnits.Clear();
            UnitBuckets.Clear();
            BookBuckets.Clear();

            Heads.Clear();
            Units.Clear();
            Books.Clear();
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
        public int GetBookCount()
        {
            int Count = 0;
            for (int i = 0; i < this.Books.Count; i++)
            {
                Count += this.Books[i].GetCount();
            }
            return Count;
        }

        public int GetCount()
        {
            return GetUnitsCount() + GetBookCount();
        }

        public void ChooseLinks()
        {
            if (CheckLinksEvent == null)
            {
                MarkHeadsPercent = 40;
                return;
            }

            MarkHeadsPercent = 0;

            List<BaseUnit> WaitDeletes = new List<BaseUnit>();
            HashSet<string> HandledInThisStage = new HashSet<string>();

            int TotalToProcess = TempUnits.Count;
            int ProcessedCount = 0;
            int UpdateInterval = Math.Max(1, TotalToProcess / 100);

            foreach (var GetItem in TempUnits)
            {
                ProcessedCount++;

                if (HandledInThisStage.Contains(GetItem.Key))
                {
                    if (ProcessedCount % UpdateInterval == 0)
                    {
                        MarkHeadsPercent = Math.Round(Math.Min(ProcessedCount, TotalToProcess) * 40.0 / TotalToProcess, 2);
                    }
                    continue;
                }

                if (GetItem.Type.ToUpper() == "BOOK")
                {
                    var GetLinks = CheckLinksEvent.Invoke(TempUnits, GetItem);

                    if (GetLinks != null)
                    {
                        var DistinctLinks = GetLinks.GroupBy(Link => Link.Key).Select(L => L.First()).ToList();
                        var FilteredLinks = DistinctLinks.Where(Link => !HandledInThisStage.Contains(Link.Key)).ToList();
                        if (FilteredLinks.Count == 0)
                        {
                            if (ProcessedCount % UpdateInterval == 0)
                            {
                                MarkHeadsPercent = Math.Round(Math.Min(ProcessedCount, TotalToProcess) * 40.0 / TotalToProcess, 2);
                            }
                            continue;
                        }

                        foreach (var Link in FilteredLinks)
                        {
                            HandledInThisStage.Add(Link.Key);
                        }

                        var Bucket = new P_Bucket(this.AddedKeys, null, 9999, 0, 1);
                        Bucket.Add(FilteredLinks, 0);
                        this.BookBuckets.Add(Bucket);

                        WaitDeletes.AddRange(FilteredLinks);
                    }
                    else
                    {
                        var Bucket = new P_Bucket(this.AddedKeys, null, 9999, 0, 0);
                        Bucket.Add(GetItem, 0);
                        this.BookBuckets.Add(Bucket);

                        WaitDeletes.Add(GetItem);
                        HandledInThisStage.Add(GetItem.Key);
                    }
                }
                else
                {
                    var GetLinks = CheckLinksEvent.Invoke(TempUnits, GetItem);

                    if (GetLinks != null)
                    {
                        if (GetLinks.Count > 1)//Only projects with more than one item can establish a sequence
                        {
                            var DistinctLinks = GetLinks.GroupBy(Link => Link.Key).Select(L => L.First()).ToList();
                            var FilteredLinks = DistinctLinks.Where(Link => !HandledInThisStage.Contains(Link.Key)).ToList();
                            if (FilteredLinks.Count == 0)
                            {
                                if (ProcessedCount % UpdateInterval == 0)
                                {
                                    MarkHeadsPercent = Math.Round(Math.Min(ProcessedCount, TotalToProcess) * 40.0 / TotalToProcess, 2);
                                }
                                continue;
                            }

                            var BucketIndex = 0;
                            List<P_Bucket> Buckets = new List<P_Bucket>();
                            Buckets.Add(new P_Bucket(this.AddedKeys, null, Phoenix.Config.BucketLengthLimit, 0, 1));

                            foreach (var Link in FilteredLinks)
                            {
                                if (!Buckets[BucketIndex].TryAdd(Link))
                                {
                                    var NewBucket = new P_Bucket(this.AddedKeys, null, Phoenix.Config.BucketLengthLimit, 0, 1);
                                    NewBucket.Next = null;

                                    Buckets[BucketIndex].Next = NewBucket;

                                    Buckets.Add(NewBucket);

                                    BucketIndex++;

                                    Buckets[BucketIndex].Add(Link, 0);
                                }

                                HandledInThisStage.Add(Link.Key);
                            }

                            foreach (var GetBucket in Buckets)
                            {
                                this.UnitBuckets.Add(GetBucket);
                            }

                            WaitDeletes.AddRange(FilteredLinks);
                        }
                    }
                }

                if (ProcessedCount % UpdateInterval == 0)
                {
                    MarkHeadsPercent = Math.Round(Math.Min(ProcessedCount, TotalToProcess) * 40.0 / TotalToProcess, 2);
                }
            }

            foreach (var GetItem in WaitDeletes)
            {
                TempUnits.Remove(GetItem);
            }

            MarkHeadsPercent = 40;
        }

        public Translator TranslatorRef;
        public P_BucketContainer(Translator TranslatorRef, List<BaseUnit> BaseUnits)
        {
            this.TranslatorRef = TranslatorRef;
            this.TempUnits.AddRange(BaseUnits);
        }

        public void Build()
        {
            ChooseLinks();
            ChooseHeads();
            BuildBuckets();

            Units = UnitBuckets.Select((Bucket, Index) => P_Bucket_Core.ConvertToUnitGroup(Bucket, Index, Bucket.Type == 1)).ToList();
            Books = BookBuckets.Select((Bucket, Index) => P_Bucket_Core.ConvertToUnitGroup(Bucket, Index, false)).ToList();

            UnitBuckets.Clear();
            BookBuckets.Clear();
            TempUnits.Clear();
        }

        public bool CheckKey(string Key)
        {
            if (AddedKeys.Contains(Key))
            {
                return true;
            }

            return false;
        }

        public double MarkHeadsPercent = 0;

        public void ChooseHeads()
        {
            int N = TempUnits.Count;
            if (N == 0)
            {
                MarkHeadsPercent = 70;
                return;
            }

            Heads.Clear();

            List<BaseUnit> LeftoverUnits = new List<BaseUnit>();
            int MaxCharsForHeadSelection = Phoenix.Config.ContextLimit;
            var FilteredItems = new List<int>();

            for (int i = 0; i < N; i++)
            {
                var Item = TempUnits[i];
                Item.TempSim = 0;

                if (!string.IsNullOrEmpty(Item.Original) && Item.Original.Length > MaxCharsForHeadSelection)
                {
                    LeftoverUnits.Add(Item);
                }
                else
                {
                    FilteredItems.Add(i);
                }
            }

            if (FilteredItems.Count == 0)
            {
                TempUnits = LeftoverUnits;
                MarkHeadsPercent = 70;
                return;
            }

            var TokensCache = new Dictionary<int, HashSet<string>>(FilteredItems.Count);
            foreach (var ItemIndex in FilteredItems)
            {
                var Token = TextTokenizer.BuildTokenSignature(TranslatorRef.From, TempUnits[ItemIndex].Original);
                TokensCache[ItemIndex] = Token.Take(10).ToHashSet();
            }

            var PrefixBuckets = new Dictionary<string, List<int>>();
            foreach (var ItemIndex in FilteredItems)
            {
                var Prefix = ContextProc.BuildPrefixKey(TempUnits[ItemIndex].Original, 3);
                if (!PrefixBuckets.TryGetValue(Prefix, out var list))
                {
                    list = new List<int>();
                    PrefixBuckets[Prefix] = list;
                }
                list.Add(ItemIndex);
            }

            int ProcessedCount = LeftoverUnits.Count;
            int TotalToProcess = N;
            int UpdateInterval = Math.Max(1, TotalToProcess / 100);

            foreach (var Bucket in PrefixBuckets.Values)
            {
                if (Bucket.Count == 0) continue;

                if (Bucket.Count == 1)
                {
                    LeftoverUnits.Add(TempUnits[Bucket[0]]);
                    ProcessedCount++;
                    continue;
                }

                int HeadIndex = ContextProc.PickContextHead(Bucket, TempUnits, TokensCache);
                var HeadItem = TempUnits[HeadIndex];
                HeadItem.TempSim = Bucket.Count - 1;

                if (!string.IsNullOrEmpty(HeadItem.Key) && !Heads.ContainsKey(HeadItem.Key))
                {
                    HeadItem.Head = true;
                    Heads[HeadItem.Key] = HeadItem;
                }
                else
                {
                    LeftoverUnits.Add(HeadItem);
                }
                ProcessedCount++;

                foreach (var ItemIndex in Bucket)
                {
                    if (ItemIndex == HeadIndex) continue;
                    LeftoverUnits.Add(TempUnits[ItemIndex]);
                    ProcessedCount++;
                }

                if (ProcessedCount % UpdateInterval == 0)
                {
                    MarkHeadsPercent = 40 + Math.Round(Math.Min(ProcessedCount, TotalToProcess) * 30.0 / TotalToProcess, 2);
                }
            }

            var SecondStageMap = new Dictionary<string, int>();
            var DemotedHeadKeys = new List<string>();

            foreach (var KV in Heads)
            {
                var Item = KV.Value;
                var Key2 = ContextProc.BuildPrefixKey(Item.Original, 2);

                if (SecondStageMap.ContainsKey(Key2))
                {
                    DemotedHeadKeys.Add(KV.Key);
                    LeftoverUnits.Add(Item);
                }
                else
                {
                    SecondStageMap[Key2] = 1;
                }
            }

            foreach (var K in DemotedHeadKeys)
            {
                Heads.Remove(K);
            }

            TempUnits = LeftoverUnits;
            MarkHeadsPercent = 70;
        }
        public void BuildBuckets()
        {
            int TotalToProcess = Heads.Count + TempUnits.Count;
            if (TotalToProcess == 0)
            {
                MarkHeadsPercent = 100;
                return;
            }

            int ProcessedCount = 0;
            int UpdateInterval = Math.Max(1, TotalToProcess / 100);

            foreach (var GetHead in Heads.Values)
            {
                var Bucket = new P_Bucket(this.AddedKeys, GetHead, Phoenix.Config.BucketLengthLimit, 0, 0);
                Bucket.HeadTokens = TextTokenizer.BuildTokenSignature(TranslatorRef.From, GetHead.Original);
                Bucket.Next = null;
                this.UnitBuckets.Add(Bucket);

                ProcessedCount++;
                if (ProcessedCount % UpdateInterval == 0)
                {
                    MarkHeadsPercent = 70 + Math.Round(Math.Min(ProcessedCount, TotalToProcess) * 30.0 / TotalToProcess, 2);
                }
            }

            if (TempUnits.Count == 0)
            {
                MarkHeadsPercent = 100;
                RemoveEmptyBuckets();
                MergeBuckets();
                return;
            }

            double SimilarityThreshold = 0.25;

            foreach (var Unit in TempUnits)
            {
                var TokensB = TextTokenizer.BuildTokenSignature(TranslatorRef.From, Unit.Original);

                if (TokensB.Count == 0)
                {
                    var NewIndependentBucket = new P_Bucket(this.AddedKeys, null, Phoenix.Config.BucketLengthLimit, 0, 0);
                    NewIndependentBucket.Add(Unit, 0);
                    this.UnitBuckets.Add(NewIndependentBucket);

                    ProcessedCount++;
                    if (ProcessedCount % UpdateInterval == 0)
                        MarkHeadsPercent = 70 + Math.Round(Math.Min(ProcessedCount, TotalToProcess) * 30.0 / TotalToProcess, 2);
                    continue;
                }

                P_Bucket BestHeadBucket = null;
                double MaxSimilarity = -1;

                foreach (var Bucket in this.UnitBuckets)
                {
                    if (Bucket.Head == null) continue;

                    var TokensA = Bucket.HeadTokens;
                    if (TokensA == null || TokensA.Count == 0) continue;

                    int IntersectCount = TokensA.Count(token => TokensB.Contains(token));
                    int UnionCount = TokensA.Count + TokensB.Count - IntersectCount;
                    double Similarity = (double)IntersectCount / UnionCount;

                    if (Similarity > MaxSimilarity)
                    {
                        MaxSimilarity = Similarity;
                        BestHeadBucket = Bucket;
                    }
                }

                if (BestHeadBucket != null && MaxSimilarity >= SimilarityThreshold)
                {
                    P_Bucket Current = BestHeadBucket;
                    P_Bucket Last = null;
                    bool Placed = false;

                    while (Current != null)
                    {
                        if (Current.TryAdd(Unit))
                        {
                            Placed = true;
                            break;
                        }
                        Last = Current;
                        Current = Current.Next;
                    }

                    if (!Placed)
                    {
                        var NewBucket = new P_Bucket(this.AddedKeys, null, Phoenix.Config.BucketLengthLimit, 0, 0);
                        NewBucket.Add(Unit, 0);

                        NewBucket.HeadTokens = BestHeadBucket.HeadTokens;
                        NewBucket.Next = null;
                        if (Last != null)
                            Last.Next = NewBucket;
                        else
                            BestHeadBucket.Next = NewBucket;
                        this.UnitBuckets.Add(NewBucket);
                    }
                }
                else
                {
                    var NewIndependentBucket = new P_Bucket(this.AddedKeys, null, Phoenix.Config.BucketLengthLimit, 0, 0);
                    NewIndependentBucket.Add(Unit, 0);
                    this.UnitBuckets.Add(NewIndependentBucket);
                }

                ProcessedCount++;
                if (ProcessedCount % UpdateInterval == 0)
                {
                    MarkHeadsPercent = 70 + Math.Round(Math.Min(ProcessedCount, TotalToProcess) * 30.0 / TotalToProcess, 2);
                }
            }

            RemoveEmptyBuckets();
            MergeBuckets();

            MarkHeadsPercent = 100;
            GC.Collect();
        }

        private void MergeBuckets()
        {
            HashSet<P_Bucket> AnchorSet = new HashSet<P_Bucket>();
            List<P_Bucket> AllAnchorBuckets = new List<P_Bucket>();
            foreach (var Root in UnitBuckets)
            {
                if (Root.Head != null || Root.Type == 1)
                {
                    var Current = Root;
                    while (Current != null)
                    {
                        if (AnchorSet.Add(Current))
                            AllAnchorBuckets.Add(Current);
                        Current = Current.Next;
                    }
                }
            }

            List<P_Bucket> OrphanBuckets = new List<P_Bucket>();
            foreach (var Bucket in UnitBuckets)
            {
                if (Bucket.Head == null && Bucket.Type == 0 && !AnchorSet.Contains(Bucket))
                {
                    OrphanBuckets.Add(Bucket);
                }
            }

            List<BaseUnit> UnplacedUnits = new List<BaseUnit>();
            if (OrphanBuckets.Count > 0)
            {
                List<BaseUnit> OrphanUnits = new List<BaseUnit>();
                foreach (var Bucket in OrphanBuckets)
                {
                    OrphanUnits.AddRange(Bucket.GetUnits());
                    Bucket.GetUnits().Clear();
                }

                foreach (var Unit in OrphanUnits)
                {
                    bool Placed = false;
                    AllAnchorBuckets.Sort((A, B) => B.RemainingSize.CompareTo(A.RemainingSize));

                    foreach (var Bucket in AllAnchorBuckets)
                    {
                        if (Phoenix.Config.StrictLinkBucketPurity && Bucket.Type == 1)
                            continue; 

                        if (Bucket.TryAdd(Unit))
                        {
                            Unit.IsFilled = true;
                            Placed = true;
                            break;
                        }
                    }

                    if (!Placed)
                        UnplacedUnits.Add(Unit);
                }

                foreach (var Bucket in OrphanBuckets)
                    UnitBuckets.Remove(Bucket);
            }

            AllAnchorBuckets.Clear();
            AnchorSet.Clear();
            foreach (var Root in UnitBuckets)
            {
                if (Root.Head != null || Root.Type == 1)
                {
                    var Current = Root;
                    while (Current != null)
                    {
                        if (AnchorSet.Add(Current))
                            AllAnchorBuckets.Add(Current);
                        Current = Current.Next;
                    }
                }
            }

            if (AllAnchorBuckets.Count > 1)
            {
                AllAnchorBuckets.Sort((A, B) => B.RemainingSize.CompareTo(A.RemainingSize));

                for (int i = AllAnchorBuckets.Count - 1; i >= 1; i--)
                {
                    var Source = AllAnchorBuckets[i];
                    var SourceUnits = Source.GetUnits();
                    if (SourceUnits.Count == 0)
                    {
                        UnitBuckets.Remove(Source);
                        AllAnchorBuckets.RemoveAt(i);
                        continue;
                    }

                    for (int j = 0; j < i; j++)
                    {
                        var Target = AllAnchorBuckets[j];

                        if (Phoenix.Config.StrictLinkBucketPurity && Target.Type == 1)
                            continue; 

                        bool IsLinkToLink = (Source.Type == 1 && Target.Type == 1);

                        if (Target.TryAdd(new List<BaseUnit>(SourceUnits)))
                        {
                            if (!IsLinkToLink)
                            {
                                foreach (var Unit in SourceUnits)
                                    Unit.IsFilled = true;
                            }

                            UnitBuckets.Remove(Source);
                            AllAnchorBuckets.RemoveAt(i);
                            break;
                        }
                    }
                }
            }

            if (UnplacedUnits.Count > 0)
            {
                var NewBucket = new P_Bucket(this.AddedKeys, null, Phoenix.Config.BucketLengthLimit, 0, 0);
                foreach (var Unit in UnplacedUnits)
                {
                    if (!NewBucket.TryAdd(Unit))
                    {
                        var OverflowBucket = new P_Bucket(this.AddedKeys, null, Phoenix.Config.BucketLengthLimit, 0, 0);
                        OverflowBucket.Add(Unit, 0);
                        UnitBuckets.Add(OverflowBucket);
                    }
                }
                if (NewBucket.GetUnits().Count > 0)
                    UnitBuckets.Add(NewBucket);
            }

            RemoveEmptyBuckets();
            GC.Collect();
        }

        private void RemoveEmptyBuckets()
        {
            for (int i = UnitBuckets.Count - 1; i >= 0; i--)
            {
                var Bucket = UnitBuckets[i];
                if (Bucket.Head == null && Bucket.GetUnits().Count == 0)
                {
                    UnitBuckets.RemoveAt(i);
                }
            }
        }
    }
    public class P_Bucket
    {
        public int Cap = 0;
        public int RemainingSize = 0;
        public int ID = 0;
        public BaseUnit Head = null;
        private List<BaseUnit> BaseUnits = new List<BaseUnit>();
        public P_Bucket Next = null;

        public int Type = 0;

        public HashSet<string> HeadTokens = new HashSet<string>();
        public P_Bucket(HashSet<string> KeysRef, BaseUnit Head, int Cap, int HeadLength, int Type)
        {
            this.Head = Head;
            this.Cap = Cap;
            this.RemainingSize = Cap;

            if (Head != null)
            {
                this.BaseUnits.Add(this.Head);
            }

            this.Type = Type;//1 is the original order bucket mode, and 0 is the traditional similarity bucket mode

            RefreshSize();
        }

        private void RefreshSize()
        {
            int Used = P_Bucket_Core.CalcBucketTokenEstimate(this);
            this.RemainingSize = this.Cap - Used;
        }

        public bool TryAdd(BaseUnit Unit)
        {
            BaseUnits.Add(Unit);
            int Used = P_Bucket_Core.CalcBucketTokenEstimate(this);
            if (Used <= this.Cap)
            {
                this.RemainingSize = this.Cap - Used;
                return true;
            }
            BaseUnits.RemoveAt(BaseUnits.Count - 1);
            return false;
        }

        public bool TryAdd(List<BaseUnit> Units)
        {
            BaseUnits.AddRange(Units);
            int Used = P_Bucket_Core.CalcBucketTokenEstimate(this);
            if (Used <= this.Cap)
            {
                this.RemainingSize = this.Cap - Used;
                return true;
            }
            BaseUnits.RemoveRange(BaseUnits.Count - Units.Count, Units.Count);
            return false;
        }

        public void Add(List<BaseUnit> Units, int Size)
        {
            this.BaseUnits.AddRange(Units);
            RefreshSize();
        }

        public void Add(BaseUnit Unit, int Size)
        {
            this.BaseUnits.Add(Unit);
            RefreshSize();
        }

        public List<BaseUnit> GetUnits()
        {
            return this.BaseUnits;
        }
    }
}