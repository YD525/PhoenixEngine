
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using PhoenixEngine.DelegateManagement;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManagement;
using static System.Net.Mime.MediaTypeNames;
using static PhoenixEngine.Bridges.NativeBridge;
using static PhoenixEngine.TranslateCore.LanguageHelper;
using static PhoenixEngine.TranslateManage.TransCore;

namespace PhoenixEngine.TranslateManage
{
    public class TranslationUnit
    {
        public int FileUniqueKey = 0;
        public int WorkEnd = 0;
        public Thread CurrentTrd;
        public double Score = 100;
        public string Key = "";
        public string Type = "";
        public string SourceText = "";
        public string TransText = "";
        public bool IsDuplicateSource = false;
        public bool Transing = false;
        public bool Leader = false;
        public bool Translated = false;
        public double TempSim = 0;
        public int MaxTry = 10;
        public string AIParam = "";
        public Languages From = Languages.Auto;
        public Languages To = Languages.Auto;

        private CancellationTokenSource TransThreadToken;

        public bool CanTrans(int State)
        {
            if (DelegateHelper.SetTranslationUnitCallBack != null)
            {
                return DelegateHelper.SetTranslationUnitCallBack(this, State);
            }

            return true;
        }

        public void StartWork(BatchTranslationCore Source)
        {
            if (!CanTrans(0))
            {
                this.WorkEnd = 2;
                return;
            }

            if (this.TransText.Trim().Length > 0)
            {
                this.WorkEnd = 2;
                return;
            }

            if (this.IsDuplicateSource)
            {
                lock (Source.SameItemsLocker)
                {
                    if (!Source.SameItems.ContainsKey(this.SourceText))
                    {
                        Source.SameItems.Add(this.SourceText, string.Empty);
                    }
                    else
                    {
                        this.Transing = false;
                        this.WorkEnd = 2;
                        CurrentTrd = null;

                        return;
                    }
                }
            }
            WorkEnd = 1;
            this.Transing = true;
            CurrentTrd = new Thread(() =>
            {
                TransThreadToken = new CancellationTokenSource();
                var Token = TransThreadToken.Token;
                try
                {
                    NextGet:

                    Token.ThrowIfCancellationRequested();

                    if (this.SourceText.Trim().Length > 0)
                    {
                        bool CanSleep = true;

                        if (!CanTrans(1))
                        {
                            this.Transing = false;
                            this.WorkEnd = 2;
                            CurrentTrd = null;

                            return;
                        }

                        var GetResult = Translator.QuickTrans(this, ref CanSleep);
                        if (GetResult.Trim().Length > 0)
                        {
                            TransText = GetResult.Trim();

                            if (!CanTrans(2))
                            {
                                EngineSelect.AIMemory.RemoveTranslation(this.From,this.To,Translator.FormatStr(this.SourceText),TransText);

                                this.TransText = string.Empty;
                                this.Transing = false;
                                this.WorkEnd = 0;

                                CurrentTrd = null;
                                return;
                            }

                            lock (Translator.TransDataLocker)
                            {
                                if (Translator.TransData.ContainsKey(this.Key))
                                {
                                    Translator.TransData[this.Key] = GetResult;
                                }
                                else
                                {
                                    Translator.TransData.Add(this.Key, GetResult);
                                }
                            }

                            if (this.IsDuplicateSource)
                            {
                                lock (Source.SameItemsLocker)
                                {
                                    if (Source.SameItems.ContainsKey(this.SourceText))
                                    {
                                        Source.SameItems[this.SourceText] = GetResult;
                                    }
                                }
                            }

                            WorkEnd = 2;

                            this.Translated = true;

                            Source.AddTranslated(this);

                            Token.ThrowIfCancellationRequested();
                        }
                        else
                        {
                            if (this.MaxTry > 0)
                            {
                                Thread.Sleep(500);
                                this.MaxTry--;

                                goto NextGet;
                            }
                            else
                            {
                                WorkEnd = 2;
                            }
                        }
                    }
                    else
                    {
                        WorkEnd = 2;
                    }
                }
                catch (OperationCanceledException)
                {
                    try
                    {
                        this.Transing = false;
                        this.CurrentTrd = null;
                    }
                    catch { }
                }
                this.Transing = false;
                this.CurrentTrd = null;
            });
            CurrentTrd.Start();
        }

        public void CancelWorkThread()
        {
            WorkEnd = 2;
            TransThreadToken?.Cancel();
        }

        public TranslationUnit(int FileUniqueKey, string Key, string Type, string SourceText, string TransText, string AIParam, Languages From, Languages To, double Score)
        {
            this.FileUniqueKey = FileUniqueKey;
            this.Key = Key;
            this.Type = Type;
            this.SourceText = SourceText;
            this.TransText = TransText;
            this.AIParam = AIParam;
            this.From = From;
            this.To = To;
            this.Score = Score;
        }
    }
    public class BatchTranslationCore
    {
        public readonly object SameItemsLocker = new object();

        public Dictionary<string, string> SameItems = new Dictionary<string, string>();

        public Dictionary<string, TranslationUnit> UnitsLeaderToTranslate = new Dictionary<string, TranslationUnit>();

        public List<TranslationUnit> UnitsToTranslate = new List<TranslationUnit>();

        public readonly object UnitsTranslatedLocker = new object();

        public Queue<TranslationUnit> UnitsTranslated = new Queue<TranslationUnit>();

        public List<string> TranslatedKeys = new List<string>();

        public int AutoThreadLimit = 0;

        public Languages DetectSourceLang = Languages.Null;

        public Languages From = Languages.Auto;
        public Languages To = Languages.Null;

        public bool IsStop = false;

        public bool SkipWordAnalysis = false;

        public BatchTranslationCore(Languages From, Languages To, List<TranslationUnit> UnitsToTranslate, bool ClearCache = false)
        {
            if (ClearCache)
            {
                Translator.ClearCache();
            }

            this.From = From;
            this.To = To;

            this.UnitsToTranslate = UnitsToTranslate;
            Init();
        }

        #region Words Analysis

        public double MarkLeadersPercent = 0;
        public int AutoLeaderTrd = 0;
        public void MarkLeadersAndSort(List<TranslationUnit> SetItems, Languages Lang)
        {
            MarkLeadersPercent = 0;

            int N = SetItems.Count;
            if (N == 0)
                return;

            UnitsLeaderToTranslate.Clear();
            UnitsToTranslate.Clear();

            int MaxCharsForLeaderSelection = Phoenix.Config.ContextLimit;

            var FilteredItems = new List<int>();

            for (int i = 0; i < N; i++)
            {
                var Item = SetItems[i];
                Item.TempSim = 0;

                if (!string.IsNullOrEmpty(Item.SourceText) &&
                    Item.SourceText.Length > MaxCharsForLeaderSelection)
                {
                    UnitsToTranslate.Add(Item);
                }
                else
                {
                    FilteredItems.Add(i);
                }
            }

            if (FilteredItems.Count == 0)
            {
                MarkLeadersPercent = 100;
                return;
            }

            var TokensCache = new Dictionary<int, HashSet<string>>(FilteredItems.Count);

            foreach (var Item in FilteredItems)
            {
                var Token = TextTokenizer.BuildTokenSignature(Lang, SetItems[Item].SourceText);
                TokensCache[Item] = Token.Take(10).ToHashSet();
            }

            var PrefixBuckets = new Dictionary<string, List<int>>();

            foreach (var Item in FilteredItems)
            {
                var Prefix = BuildPrefixKey(SetItems[Item].SourceText, 3);

                if (!PrefixBuckets.TryGetValue(Prefix, out var List))
                {
                    List = new List<int>();
                    PrefixBuckets[Prefix] = List;
                }

                List.Add(Item);
            }

            int ProcessedCount = UnitsToTranslate.Count;
            int TotalToProcess = N;
            int UpdateInterval = Math.Max(1, TotalToProcess / 100);

            foreach (var Bucket in PrefixBuckets.Values)
            {
                if (Bucket.Count == 0)
                    continue;

                if (Bucket.Count == 1)
                {
                    UnitsToTranslate.Add(SetItems[Bucket[0]]);
                    ProcessedCount++;
                    continue;
                }

                int LeaderIndex = PickContextLeader(Bucket, SetItems, TokensCache);
                var LeaderItem = SetItems[LeaderIndex];

                LeaderItem.TempSim = Bucket.Count - 1;

                if (!string.IsNullOrEmpty(LeaderItem.Key))
                {
                    LeaderItem.Leader = true;
                    UnitsLeaderToTranslate[LeaderItem.Key] = LeaderItem;
                }
                else
                {
                    UnitsToTranslate.Add(LeaderItem);
                }

                ProcessedCount++;

                foreach (var Item in Bucket)
                {
                    if (Item == LeaderIndex)
                        continue;

                    UnitsToTranslate.Add(SetItems[Item]);
                    ProcessedCount++;
                }

                if (ProcessedCount % UpdateInterval == 0)
                {
                    MarkLeadersPercent = Math.Round(Math.Min(ProcessedCount, TotalToProcess) * 100.0 / TotalToProcess, 2);
                }
            }

            var SecondStageMap = new Dictionary<string, int>();
            var RemoveLeaders = new List<string>();

            foreach (var KV in UnitsLeaderToTranslate)
            {
                var Item = KV.Value;
                var Key2 = BuildPrefixKey(Item.SourceText, 2);

                if (SecondStageMap.ContainsKey(Key2))
                {
                    UnitsToTranslate.Add(Item);
                    RemoveLeaders.Add(KV.Key);
                }
                else
                {
                    SecondStageMap[Key2] = 1;
                }
            }

            foreach (var K in RemoveLeaders)
            {
                UnitsLeaderToTranslate.Remove(K);
            }

            if (UnitsLeaderToTranslate.Count < 1500)
            {
                AutoLeaderTrd = SortLeadersAndCalculateThreads(DetectSourceLang, Phoenix.Config.MaxThreadCount, ref UnitsLeaderToTranslate);
            }
            else
            {
                AutoLeaderTrd = 3;
            }
           
            MarkLeadersPercent = 100;
        }

        private int SortLeadersAndCalculateThreads(Languages Lang, int NeedTrd, ref Dictionary<string, TranslationUnit> Leaders)
        {
            var SortedPairs = new List<KeyValuePair<string, TranslationUnit>>();
            var TokenMap = new Dictionary<TranslationUnit, HashSet<string>>();

            foreach (var kv in Leaders)
            {
                var Unit = kv.Value;
                SortedPairs.Add(kv);

                TokenMap[Unit] = TextTokenizer.BuildTokenSignature(Lang, Unit.SourceText);
            }

            // Sort leaders by priority (Tokens count -> TempSim -> Length)
            SortedPairs.Sort((A, B) =>
            {
                var UA = A.Value;
                var UB = B.Value;

                int C = TokenMap[UB].Count.CompareTo(TokenMap[UA].Count);
                if (C != 0) return C;

                C = UB.TempSim.CompareTo(UA.TempSim);
                if (C != 0) return C;

                return UB.SourceText.Length.CompareTo(UA.SourceText.Length);
            });

            // --- Step 1: Build conflict matrix ---
            int N = SortedPairs.Count;
            bool[,] ConflictMatrix = new bool[N, N];

            for (int i = 0; i < N; i++)
            {
                var Ti = TokenMap[SortedPairs[i].Value];
                for (int j = i + 1; j < N; j++)
                {
                    var Tj = TokenMap[SortedPairs[j].Value];
                    int overlap = Ti.Count(t => Tj.Contains(t));
                    if (overlap >= 2) // Strong conflict threshold
                        ConflictMatrix[i, j] = ConflictMatrix[j, i] = true;
                }
            }

            // --- Step 2: Initialize threads ---
            var Threads = new List<List<KeyValuePair<string, TranslationUnit>>>();
            int ThreadCount = Math.Min(NeedTrd, Math.Max(1, (int)Math.Sqrt(N))); //Empirical formula for initial thread count
            for (int i = 0; i < ThreadCount; i++)
                Threads.Add(new List<KeyValuePair<string, TranslationUnit>>());

            // --- Step 3: Greedy assignment with global minimal conflict ---
            for (int idx = 0; idx < N; idx++)
            {
                var LeaderPair = SortedPairs[idx];

                int BestThread = 0;
                int MinConflictCount = int.MaxValue;

                for (int t = 0; t < Threads.Count; t++)
                {
                    int ConflictCount = 0;
                    foreach (var Existing in Threads[t])
                    {
                        int existingIdx = SortedPairs.IndexOf(Existing);
                        if (ConflictMatrix[idx, existingIdx])
                            ConflictCount++;
                    }

                    if (ConflictCount < MinConflictCount)
                    {
                        MinConflictCount = ConflictCount;
                        BestThread = t;
                    }
                }

                Threads[BestThread].Add(LeaderPair);
            }

            // --- Step 4: Optional: redistribute to fill <= NeedTrd threads ---
            while (Threads.Count > NeedTrd)
            {
                int mergeA = 0, mergeB = 1;
                int minOverlap = int.MaxValue;

                for (int i = 0; i < Threads.Count; i++)
                {
                    for (int j = i + 1; j < Threads.Count; j++)
                    {
                        int overlap = 0;
                        foreach (var a in Threads[i])
                            foreach (var b in Threads[j])
                                if (ConflictMatrix[SortedPairs.IndexOf(a), SortedPairs.IndexOf(b)])
                                    overlap++;
                        if (overlap < minOverlap)
                        {
                            minOverlap = overlap;
                            mergeA = i;
                            mergeB = j;
                        }
                    }
                }

                Threads[mergeA].AddRange(Threads[mergeB]);
                Threads.RemoveAt(mergeB);
            }

            // --- Step 5: Rebuild Leaders dictionary in sorted order ---
            var NewLeaders = new Dictionary<string, TranslationUnit>(Leaders.Count);
            foreach (var thread in Threads)
                foreach (var kv in thread)
                    NewLeaders[kv.Key] = kv.Value;

            Leaders = NewLeaders;

            // Return approximate minimal thread count, with slight tolerated conflicts
            return Threads.Count;
        }

        private static int CountWords(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            int count = 0;
            bool inWord = false;

            foreach (char c in text)
            {
                if (c == ' ' || c == '\t')
                    inWord = false;
                else if (!inWord)
                {
                    count++;
                    inWord = true;
                }
            }

            return count;
        }

        private static int PickContextLeader(
            List<int> bucket,
            List<TranslationUnit> items,
            Dictionary<int, HashSet<string>> tokensCache)
        {
            int best = -1;

            int bestWordCount = int.MaxValue;
            bool bestHasDigit = true;
            int bestTokenCount = -1;
            int bestLength = int.MaxValue;

            foreach (var i in bucket)
            {
                var text = items[i].SourceText;

                int wc = CountWords(text);
                bool hasDigit = HasDigit(text);
                int tc = tokensCache[i].Count;
                int len = text.Length;

                if (
                    wc < bestWordCount ||
                    (wc == bestWordCount && bestHasDigit && !hasDigit) ||
                    (wc == bestWordCount && hasDigit == bestHasDigit && tc > bestTokenCount) ||
                    (wc == bestWordCount && hasDigit == bestHasDigit && tc == bestTokenCount && len < bestLength)
                )
                {
                    best = i;
                    bestWordCount = wc;
                    bestHasDigit = hasDigit;
                    bestTokenCount = tc;
                    bestLength = len;
                }
            }

            return best;
        }

        private static string BuildPrefixKey(string text, int maxWords)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            int len = text.Length;
            int wordCount = 0;
            int i = 0;

            var sb = new StringBuilder(len);

            while (i < len && wordCount < maxWords)
            {
                while (i < len && (text[i] == ' ' || text[i] == '\t'))
                    i++;

                if (i >= len)
                    break;

                if (wordCount > 0)
                    sb.Append(' ');

                while (i < len && text[i] != ' ' && text[i] != '\t')
                {
                    sb.Append(char.ToLowerInvariant(text[i]));
                    i++;
                }

                wordCount++;
            }

            return sb.ToString();
        }

        private static bool HasDigit(string text)
        {
            foreach (char c in text)
                if (char.IsDigit(c))
                    return true;
            return false;
        }

        #endregion

        public List<BatchTranslationUnit> MergeAll()
        {
            return TranslationUnitBatcher.MergeUnits(this.UnitsLeaderToTranslate, this.UnitsToTranslate);
        }

        public ThreadUsageInfo ThreadUsage = new ThreadUsageInfo();

        public readonly object TranslatedAddLocker = new object();

        public void AddTranslated(TranslationUnit Item)
        {
            int MaxTry = 10;
            lock (TranslatedAddLocker)
            {
                bool HasAdd = false;
                bool HasUPDate = false;
                try
                {
                    //Has Add
                    UnitsTranslated.Enqueue(Item);
                    HasAdd = true;
                    TranslatorBridge.SetCloudTransData(Item.Key, Item.SourceText, Item.TransText);
                    HasUPDate = true;
                    TranslatedKeys.Add(Item.Key);
                }
                catch
                {
                    NextTry:

                    if (!HasAdd)
                    {
                        try
                        {
                            if (MaxTry > 0)
                            {
                                UnitsTranslated.Enqueue(Item);
                                MaxTry--;
                                HasAdd = true;
                            }

                        }
                        catch { }
                    }
                    if (!HasUPDate)
                    {
                        try
                        {
                            if (MaxTry > 0)
                            {
                                TranslatorBridge.SetCloudTransData(Item.Key, Item.SourceText, Item.TransText);
                                MaxTry--;
                                HasUPDate = true;
                            }

                        }
                        catch { }
                    }
                    if ((!HasAdd || !HasUPDate) && MaxTry > 0)
                    {
                        Thread.Sleep(100);
                        goto NextTry;
                    }
                }
            }
        }

        public object WaitTranslateLock = new object();

        public int GetWorkCount()
        {
            int WorkCount = 0;

            for (int i = 0; i < UnitsToTranslate.Count; i++)
            {
                if (UnitsToTranslate[i].Transing)
                {
                    WorkCount++;
                }
            }

            foreach (var kvp in UnitsLeaderToTranslate)
            {
                if (kvp.Value.Transing)
                {
                    WorkCount++;
                }
            }

            return WorkCount;
        }

        public void MarkDuplicates(List<TranslationUnit> Items)
        {
            var CountDict = new Dictionary<string, int>();

            foreach (var Item in Items)
            {
                string Key = Item.SourceText ?? "";
                if (CountDict.ContainsKey(Key))
                    CountDict[Key]++;
                else
                    CountDict[Key] = 1;
            }

            foreach (var Item in Items)
            {
                string Key = Item.SourceText ?? "";
                Item.IsDuplicateSource = CountDict[Key] > 1;
            }
        }

        public void Init()
        {
            WorkState = 0;
            UnitsLeaderToTranslate.Clear();

            lock (SameItemsLocker)
            {
                SameItems.Clear();
            }

            lock (TranslatedAddLocker)
            {
                UnitsTranslated.Clear();
                TranslatedKeys.Clear();
            }

            MarkDuplicates(UnitsToTranslate);

            if (Phoenix.Config.MaxThreadCount <= 0)
            {
                Phoenix.Config.MaxThreadCount = 1;
            }

            AutoSleep = 1;
        }

        public CancellationTokenSource TransMainTrdCancel = null;
        public Thread TransMainTrd = null;

        public void CancelMainTransThread()
        {
            TransMainTrdCancel?.Cancel();
        }

        public int AutoSleep = 1;

        public bool IsWork = false;

        public int WorkState = 0;

        public void SetEndState()
        {
            IsWork = false;
            TransMainTrd = null;

            try
            {
                WorkState = -1;
            }
            catch { }
        }

        public TranslationUnit GetWaitTransUnit(ref List<TranslationUnit> Arrays)
        {
            lock (WaitTranslateLock)
            {
                return Arrays.FirstOrDefault(u => u.WorkEnd <= 0);
            }
        }

        public TranslationUnit GetWaitTransUnitFromDict(Dictionary<string, TranslationUnit> Dict)
        {
            lock (WaitTranslateLock)
            {
                foreach (var kvp in Dict)
                {
                    if (kvp.Value.WorkEnd <= 0)
                    {
                        return kvp.Value;
                    }
                }
                return null;
            }
        }

        public int AddWaitTransUnit(TranslationUnit Item, bool IsLeader = false)
        {
            lock (WaitTranslateLock)
            {
                bool HasAdd = false;
                try
                {
                    int Count = 0;
                    if (IsLeader)
                    {
                        if (!string.IsNullOrEmpty(Item.Key))
                        {
                            UnitsLeaderToTranslate[Item.Key] = Item;
                            HasAdd = true;
                            Count = UnitsLeaderToTranslate.Count;
                        }
                    }
                    else
                    {
                        UnitsToTranslate.Add(Item);
                        HasAdd = true;
                        Count = UnitsToTranslate.Count;
                    }

                    return Count;
                }
                catch
                {
                    if (!HasAdd)
                    {
                        return -1;
                    }

                    return 0;
                }
            }
        }

        public void MarkLeaders()
        {
            if (!SkipWordAnalysis)
            {
                WorkState = 0;
                DetectSource();
                MarkLeadersAndSort(new List<TranslationUnit>(this.UnitsToTranslate), this.DetectSourceLang);
                WorkState = 1;
            }
            else
            {
                WorkState = 2;
            }
        }

        public void ReSet()
        {
            for (int i = 0; i < this.UnitsLeaderToTranslate.Count; i++)
            {
                string GetKey = this.UnitsLeaderToTranslate.ElementAt(i).Key;
                this.UnitsLeaderToTranslate[GetKey].Translated = false;
                this.UnitsLeaderToTranslate[GetKey].WorkEnd = 0;
                this.UnitsLeaderToTranslate[GetKey].TransText = string.Empty;
                this.UnitsLeaderToTranslate[GetKey].From = Phoenix.From;
                this.UnitsLeaderToTranslate[GetKey].To = Phoenix.To;
            }

            for (int i = 0; i < this.UnitsToTranslate.Count; i++)
            {
                this.UnitsToTranslate[i].Translated = false;
                this.UnitsToTranslate[i].WorkEnd = 0;
                this.UnitsToTranslate[i].TransText = string.Empty;
                this.UnitsToTranslate[i].From = Phoenix.From;
                this.UnitsToTranslate[i].To = Phoenix.To;
            }
        }

        public void DetectSource()
        {
            if (this.From != Languages.Auto)
            {
                this.DetectSourceLang = this.From;
            }
            else
            {
                FileLanguageDetect LangDetecter = new FileLanguageDetect();

                for (int i = 0; i < this.UnitsToTranslate.Count; i++)
                {
                    LangDetecter.DetectLanguageByFile(this.UnitsToTranslate[i].SourceText);
                }

                this.DetectSourceLang = LangDetecter.GetLang();

                LangDetecter = null;
            }
        }

        public void Start()
        {
            if (IsWork || TransMainTrd == null)
            {
                ExitAny = false;
                TransMainTrd = new Thread(() =>
                {
                    IsWork = true;

                    ReSet();

                    DetectSource();

                    if (ExitAny)
                    {
                        SetEndState();
                        return;
                    }

                    TransMainTrdCancel = new CancellationTokenSource();
                    var Token = TransMainTrdCancel.Token;

                    int CurrentTrds = 0;

                    bool IsLeader = true;

                    WorkState = 2;

                    while (true)
                    {
                        if (!IsStop)
                        {
                            try
                            {
                                NextFind:

                                ThreadUsage.CurrentThreads = CurrentTrds;
                                ThreadUsage.MaxThreads = Phoenix.Config.MaxThreadCount;

                                bool CanExit = true;
                                Token.ThrowIfCancellationRequested();
                                CurrentTrds = GetWorkCount();
                                
                                int AutoTrd = Phoenix.Config.MaxThreadCount;

                                if (IsLeader)
                                {
                                    if (AutoLeaderTrd <= 0)
                                    {
                                        AutoLeaderTrd = 1;
                                    }
                                    AutoTrd = AutoLeaderTrd;
                                }

                                if (CurrentTrds < AutoTrd)
                                {
                                    TranslationUnit Leader = GetWaitTransUnitFromDict(UnitsLeaderToTranslate);
                                    if (Leader != null)
                                    {
                                        Leader.StartWork(this);
                                        CanExit = false;
                                        IsLeader = true;
                                        goto Next;
                                    }

                                    TranslationUnit Normal = GetWaitTransUnit(ref UnitsToTranslate);
                                    if (Normal != null)
                                    {
                                        Normal.StartWork(this);
                                        CanExit = false;
                                        IsLeader = false;
                                        goto Next;
                                    }

                                    Next:

                                    if (CurrentTrds > Phoenix.Config.MaxThreadCount * Phoenix.Config.ThrottleRatio)
                                    {
                                        AutoSleep = Phoenix.Config.ThrottleDelayMs;
                                    }
                                    else
                                    {
                                        AutoSleep = 0;
                                    }

                                    if (AutoSleep > 0)
                                    {
                                        Thread.Sleep(AutoSleep);
                                    }
                                }

                                if (CanExit)
                                {
                                    int SucessCount = 0;

                                    for (int i = 0; i < UnitsToTranslate.Count; i++)
                                    {
                                        if (UnitsToTranslate[i].WorkEnd == 2)
                                        {
                                            SucessCount++;
                                        }
                                    }

                                    foreach (var kvp in UnitsLeaderToTranslate)
                                    {
                                        if (kvp.Value.WorkEnd == 2)
                                        {
                                            SucessCount++;
                                        }
                                    }

                                    if (SucessCount == (UnitsToTranslate.Count + UnitsLeaderToTranslate.Count))
                                    {
                                        if (SameItems != null)
                                        {
                                            if (SameItems.Count > 0)
                                            {
                                                for (int i = 0; i < SameItems.Count; i++)
                                                {
                                                    string GetKey = SameItems.ElementAt(i).Key;
                                                    SetDuplicateSource(GetKey);
                                                }
                                            }
                                        }

                                        IsWork = false;

                                        WorkState = 3;

                                        Close();

                                        return;
                                    }
                                    else
                                    {
                                        Thread.Sleep(1);
                                        goto NextFind;
                                    }
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                IsWork = false;
                                TransMainTrd = null;

                                try
                                {
                                    WorkState = -1;
                                }
                                catch { }
                                return;
                            }
                        }
                        else
                        {
                            Thread.Sleep(500);
                        }
                        Thread.Sleep(1);
                    }

                });

                TransMainTrd.Start();
            }
        }

        public bool ExitAny = false;

        public void Clear()
        {
            for (int i = 0; i < UnitsLeaderToTranslate.Count; i++)
            {
                var Key = UnitsLeaderToTranslate.ElementAt(i).Key;
                UnitsLeaderToTranslate[Key].TransText = string.Empty;
                UnitsLeaderToTranslate[Key].Transing = false;
                UnitsLeaderToTranslate[Key].From = Phoenix.From;
                UnitsLeaderToTranslate[Key].To = Phoenix.To;
                UnitsLeaderToTranslate[Key].WorkEnd = 0;
                UnitsLeaderToTranslate[Key].Translated = false;
                UnitsToTranslate[i].IsDuplicateSource = false;
            }

            for (int i = 0; i < UnitsToTranslate.Count; i++)
            {
                UnitsToTranslate[i].TransText = string.Empty;
                UnitsToTranslate[i].Transing = false;
                UnitsToTranslate[i].From = Phoenix.From;
                UnitsToTranslate[i].To = Phoenix.To;
                UnitsToTranslate[i].WorkEnd = 0;
                UnitsToTranslate[i].Translated = false;
                UnitsToTranslate[i].IsDuplicateSource = false;
            }
           
            SameItems.Clear();
        }
        public void Close()
        {
            ExitAny = true;
            try
            {
                CancelMainTransThread();
            }
            catch { }

            for (int i = 0; i < UnitsToTranslate.Count; i++)
            {
                if (UnitsToTranslate[i].Transing)
                {
                    try
                    {
                        UnitsToTranslate[i].CancelWorkThread();
                    }
                    catch { }

                    try
                    {
                        if (UnitsToTranslate[i].CurrentTrd != null)
                        {
                            UnitsToTranslate[i].CurrentTrd.Abort();
                        }

                        UnitsToTranslate[i].CurrentTrd = null;
                    }
                    catch { }
                }
            }

            foreach (var Kvp in UnitsLeaderToTranslate)
            {
                if (Kvp.Value.Transing)
                {
                    try
                    {
                        Kvp.Value.CancelWorkThread();
                       
                    }
                    catch { }

                    try
                    {
                        if (Kvp.Value.CurrentTrd != null)
                        {
                            Kvp.Value.CurrentTrd.Abort();
                        }

                        Kvp.Value.CurrentTrd = null;
                    }
                    catch { }
                }
            }

            Clear();
            TransMainTrd = null;
        }

        public void Keep()
        {
            if (IsStop)
            {
                IsStop = false;
            }
        }

        public void Stop()
        {
            IsStop = true;
        }

        public void SetDuplicateSource(string Source)
        {
            IEnumerable<TranslationUnit> AllUnits = UnitsToTranslate.Concat(UnitsLeaderToTranslate.Values);

            foreach (var Unit in AllUnits)
            {
                if (Unit.SourceText == Source && !TranslatedKeys.Contains(Unit.Key))
                {
                    lock (Translator.TransDataLocker)
                    {
                        Translator.TransData[Unit.Key] = SameItems[Source];
                        TranslatorBridge.SetCloudTransData(Unit.Key, Source, SameItems[Source]);
                    }

                    lock (TranslatedAddLocker)
                    {
                        UnitsTranslated.Enqueue(Unit);
                        TranslatedKeys.Add(Unit.Key);
                    }
                }
            }
        }

        public TranslationUnit DequeueTranslated(out bool IsEnd)
        {
            try
            {
                lock (UnitsTranslatedLocker)
                {
                    if (UnitsTranslated.Count > 0)
                    {
                        var Item = UnitsTranslated.Dequeue();

                        if (!string.IsNullOrWhiteSpace(Item.TransText))
                        {
                            IsEnd = false;
                            return Item;
                        }

                        IsEnd = false;
                        return null;
                    }

                    bool NoMoreWork = (this.WorkState == 3 && GetWorkCount() == 0);

                    IsEnd = NoMoreWork;

                    return null;
                }
            }
            catch
            {
                IsEnd = false;
                return null;
            }
        }
    }
}
