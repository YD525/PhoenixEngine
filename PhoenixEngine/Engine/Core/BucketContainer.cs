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

        public static int BucketLengthLimit = 3000;

        public Dictionary<string, BaseUnit> Heads = new Dictionary<string, BaseUnit>();

        public List<BaseUnit> TempUnits = new List<BaseUnit>();
        public List<P_Bucket> UnitBuckets = new List<P_Bucket>();
        public List<P_Bucket> BookBuckets = new List<P_Bucket>();

        public List<UnitGroup> Units = new List<UnitGroup>();
        public List<UnitGroup> Books = new List<UnitGroup>();

        public delegate List<BaseUnit> CheckLinks(List<BaseUnit> TempUnits,BaseUnit Unit);
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
            if (CheckLinksEvent == null) return;

            List<BaseUnit> WaitDeletes = new List<BaseUnit>();
            HashSet<string> HandledInThisStage = new HashSet<string>();

            foreach (var GetItem in TempUnits)
            {
                if (HandledInThisStage.Contains(GetItem.Key))
                    continue;

                if (GetItem.Type.ToUpper() == "BOOK")
                {
                    var GetLinks = CheckLinksEvent.Invoke(TempUnits, GetItem);

                    if (GetLinks != null)
                    {
                        int TotalSize = 0;
                        foreach (var Link in GetLinks)
                        {
                            TotalSize += CalcTokenLength(Link.Original, P_Language.DetectLanguageByLine(Link.Original));
                            HandledInThisStage.Add(Link.Key);
                        }

                        var Bucket = new P_Bucket(this.AddedKeys, null, 9999, 0);
                        Bucket.Add(GetLinks, 0);
                        this.BookBuckets.Add(Bucket);

                        WaitDeletes.AddRange(GetLinks);
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
                        int TotalSize = 0;
                        foreach (var Link in GetLinks)
                        {
                            TotalSize += CalcTokenLength(Link.Original, P_Language.DetectLanguageByLine(Link.Original));
                            HandledInThisStage.Add(Link.Key);
                        }

                        var Bucket = new P_Bucket(this.AddedKeys, null, P_BucketContainer.BucketLengthLimit, 0);
                        Bucket.Add(GetLinks, TotalSize);
                        this.UnitBuckets.Add(Bucket);

                        WaitDeletes.AddRange(GetLinks);
                    }
                }
            }

            foreach (var GetItem in WaitDeletes)
            {
                TempUnits.Remove(GetItem);
            }
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

        public double MarkHeadsPercent = 0;

        public void ChooseHeads()
        {
            int N = TempUnits.Count;
            if (N == 0)
            {
                MarkHeadsPercent = 100;
                return;
            }

            MarkHeadsPercent = 0;
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
                MarkHeadsPercent = 100;
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
                    MarkHeadsPercent = Math.Round(Math.Min(ProcessedCount, TotalToProcess) * 100.0 / TotalToProcess, 2);
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
            MarkHeadsPercent = 100;
        }
        public void BuildBuckets()
        {
            foreach (var GetHead in Heads.Values)
            {
                int HeadSize = CalcTokenLength(GetHead.Original, P_Language.DetectLanguageByLine(GetHead.Original));

                var Bucket = new P_Bucket(this.AddedKeys, GetHead, P_BucketContainer.BucketLengthLimit, HeadSize);

                Bucket.HeadTokens = TextTokenizer.BuildTokenSignature(TranslatorRef.From, GetHead.Original);

                this.UnitBuckets.Add(Bucket);
            }

            if (TempUnits.Count == 0)
                return;

            double SimilarityThreshold = 0.25;

            foreach (var Unit in TempUnits)
            {
                P_Bucket BestBucket = null;
                double MaxSimilarity = -1;
                int UnitSize = CalcTokenLength(Unit.Original, TranslatorRef.From);

                var TokensB = TextTokenizer.BuildTokenSignature(TranslatorRef.From, Unit.Original);
                if (TokensB.Count == 0)
                {
                    var NewIndependentBucket = new P_Bucket(this.AddedKeys, null, P_BucketContainer.BucketLengthLimit, 0);
                    NewIndependentBucket.Add(Unit, UnitSize);
                    this.UnitBuckets.Add(NewIndependentBucket);
                    continue;
                }

                foreach (var Bucket in this.UnitBuckets)
                {
                    if (Bucket.Head == null)
                        continue;

                    if (Bucket.RemainingSize < UnitSize)
                        continue;

                    var TokensA = Bucket.HeadTokens;
                    if (TokensA == null || TokensA.Count == 0)
                        continue;

                    int IntersectCount = 0;
                    foreach (var token in TokensA)
                    {
                        if (TokensB.Contains(token))
                            IntersectCount++;
                    }

                    int UnionCount = TokensA.Count + TokensB.Count - IntersectCount;
                    double Similarity = (double)IntersectCount / UnionCount;

                    if (Similarity > MaxSimilarity)
                    {
                        MaxSimilarity = Similarity;
                        BestBucket = Bucket;
                    }
                }

                if (BestBucket != null && MaxSimilarity >= SimilarityThreshold)
                {
                    BestBucket.Add(Unit, UnitSize);
                }
                else
                {
                    var NewIndependentBucket = new P_Bucket(this.AddedKeys, null, P_BucketContainer.BucketLengthLimit, 0);
                    NewIndependentBucket.Add(Unit, UnitSize);

                    NewIndependentBucket.HeadTokens = new HashSet<string>();

                    this.UnitBuckets.Add(NewIndependentBucket);
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
            return Group;
        }
    }
    public class P_Bucket
    {
        public int RemainingSize = 0;
        public int ID = 0;
        public BaseUnit Head = null;//"Leader" doesn't sound great; "Head" would be better.
        private List<BaseUnit> BaseUnits = new List<BaseUnit>();
        public int Next = 0;

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