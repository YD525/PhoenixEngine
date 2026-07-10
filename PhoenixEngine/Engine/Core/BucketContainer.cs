using System;
using System.Collections.Generic;
using System.Linq;
using PhoenixEngine.Language;
using PhoenixEngine.Sequence;
using PhoenixEngine.Translate;
using PhoenixEngine.Unit;

namespace PhoenixEngine.Engine
{
    public class P_BucketContainer
    {
        public HashSet<string> AddedKeys = new HashSet<string>();

        public static int BucketLengthLimit = 3900;

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

                        int TotalSize = 0;
                        var SeenInThisBatch = new HashSet<string>();
                        foreach (var Link in FilteredLinks)
                        {
                            bool IsDuplicate = !SeenInThisBatch.Add(Link.Original);
                            TotalSize += CalcTokenLength(Link.Original, P_Language.DetectLanguageByLine(Link.Original), false, IsDuplicate);
                            HandledInThisStage.Add(Link.Key);
                        }

                        var Bucket = new P_Bucket(this.AddedKeys, null, 9999, 0);
                        Bucket.Add(FilteredLinks, 0);
                        this.BookBuckets.Add(Bucket);

                        WaitDeletes.AddRange(FilteredLinks);
                    }
                    else
                    {
                        var Bucket = new P_Bucket(this.AddedKeys, null, 9999, 0);
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
                        Buckets.Add(new P_Bucket(this.AddedKeys, null, P_BucketContainer.BucketLengthLimit, 0));

                        var SeenInCurrentBucket = new HashSet<string>();

                        foreach (var Link in FilteredLinks)
                        {
                            bool IsDuplicate = !SeenInCurrentBucket.Add(Link.Original);
                            var TokenSize = CalcTokenLength(Link.Original, P_Language.DetectLanguageByLine(Link.Original), true, IsDuplicate);

                            if (Buckets[BucketIndex].RemainingSize >= TokenSize)
                            {
                                Buckets[BucketIndex].Add(Link, TokenSize);
                            }
                            else
                            {
                                var NewBucket = new P_Bucket(this.AddedKeys, null, P_BucketContainer.BucketLengthLimit, 0);
                                NewBucket.Next = null;

                                Buckets[BucketIndex].Next = NewBucket;

                                Buckets.Add(NewBucket);

                                BucketIndex++;

                                SeenInCurrentBucket = new HashSet<string>();
                                SeenInCurrentBucket.Add(Link.Original);
                                Buckets[BucketIndex].Add(Link, TokenSize);
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

            Units = UnitBuckets.Select((Bucket, Index) => ConvertToUnitGroup(Bucket, Index)).ToList();
            Books = BookBuckets.Select((Bucket, Index) => ConvertToUnitGroup(Bucket, Index)).ToList();

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

        public static int CalcTokenLength(string Text, Languages From, bool IncludeTag, bool IsDuplicate)
        {
            if (string.IsNullOrEmpty(Text))
                return 0;

            if (IsDuplicate)
                return 0;

            int RawLength = Text.Length;

            int TextLength;
            switch (From)
            {
                case Languages.SimplifiedChinese:
                case Languages.TraditionalChinese:
                case Languages.Japanese:
                case Languages.Korean:
                    TextLength = (int)(RawLength * 2.5);
                    break;

                case Languages.Thai:
                case Languages.Hindi:
                case Languages.Urdu:
                case Languages.Persian:
                case Languages.Russian:
                case Languages.Ukrainian:
                    TextLength = (int)(RawLength * 1.5);
                    break;

                default:
                    TextLength = RawLength;
                    break;
            }

            if (IncludeTag)
            {
                const int TagLength = 30;
                return TextLength + TagLength;
            }

            return TextLength;
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
            this.UnitBuckets.Clear();

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
                int HeadSize = CalcTokenLength(GetHead.Original, TranslatorRef.From,true, false);
                var Bucket = new P_Bucket(this.AddedKeys, GetHead, P_BucketContainer.BucketLengthLimit, HeadSize);
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
                return;
            }

            double SimilarityThreshold = 0.25;

            foreach (var Unit in TempUnits)
            {
                int UnitSize = CalcTokenLength(Unit.Original, TranslatorRef.From,true, false);
                var TokensB = TextTokenizer.BuildTokenSignature(TranslatorRef.From, Unit.Original);

                if (TokensB.Count == 0)
                {
                    var NewIndependentBucket = new P_Bucket(this.AddedKeys, null, P_BucketContainer.BucketLengthLimit, 0);
                    NewIndependentBucket.Add(Unit, UnitSize);
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
                        bool IsDuplicate = false;
                        var ExistingUnits = Current.GetUnits();
                        foreach (var ExistingUnit in ExistingUnits)
                        {
                            if (ExistingUnit.Original == Unit.Original)
                            {
                                IsDuplicate = true;
                                break;
                            }
                        }

                        int ActualUnitSize = CalcTokenLength(Unit.Original, TranslatorRef.From,true, IsDuplicate);

                        if (Current.RemainingSize >= ActualUnitSize)
                        {
                            Current.Add(Unit, ActualUnitSize);
                            Placed = true;
                            break;
                        }
                        Last = Current;
                        Current = Current.Next;
                    }

                    if (!Placed)
                    {
                        var NewBucket = new P_Bucket(this.AddedKeys, null, P_BucketContainer.BucketLengthLimit, 0);
                        NewBucket.Add(Unit, UnitSize);
                        NewBucket.HeadTokens = new HashSet<string>();
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
                    var NewIndependentBucket = new P_Bucket(this.AddedKeys, null, P_BucketContainer.BucketLengthLimit, 0);
                    NewIndependentBucket.Add(Unit, UnitSize);
                    this.UnitBuckets.Add(NewIndependentBucket);
                }

                ProcessedCount++;
                if (ProcessedCount % UpdateInterval == 0)
                {
                    MarkHeadsPercent = 70 + Math.Round(Math.Min(ProcessedCount, TotalToProcess) * 30.0 / TotalToProcess, 2);
                }
            }

            RemoveEmptyBuckets();
            MarkHeadsPercent = 100;
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

        public static UnitGroup ConvertToUnitGroup(P_Bucket Bucket, int Index)
        {
            var Group = new UnitGroup();
            var Units = Bucket.GetUnits();
            if (Bucket.Head != null)
            {
                Group.Mode = AggregationMode.Aggregation;
                Group.Key = Index.ToString();
            }
            else
            {
                Group.Mode = AggregationMode.Single;
                Group.Key = Units.Count > 0 ? Units[0].Key : string.Empty;
            }

            Group.Units = new List<BaseUnit>(Units);
            Group.Bucket = Bucket;

            return Group;
        }
    }
    public class P_Bucket
    {
        public int RemainingSize = 0;
        public int ID = 0;
        public BaseUnit Head = null;
        private List<BaseUnit> BaseUnits = new List<BaseUnit>();
        public P_Bucket Next = null;

        public HashSet<string> HeadTokens = new HashSet<string>();
        public P_Bucket(HashSet<string> KeysRef, BaseUnit Head, int RemainingSize, int HeadLength)
        {
            this.Head = Head;

            this.RemainingSize = RemainingSize;

            if (HeadLength > 0)
                this.RemainingSize -= HeadLength;


            if (Head != null)
            {
                this.BaseUnits.Add(this.Head);
            }
        }

        public void Add(List<BaseUnit> Units, int Size)
        {
            this.RemainingSize -= Size;

            this.BaseUnits.AddRange(Units);
        }

        public void Add(BaseUnit Unit, int Size)
        {
            this.RemainingSize -= Size;

            this.BaseUnits.Add(Unit);
        }

        public List<BaseUnit> GetUnits()
        { 
           return this.BaseUnits;
        }
    }
}