
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using PhoenixEngine.DelegateManagement;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManagement;
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

        public void MarkLeadersAndSort(List<TranslationUnit> SetItems, Languages Lang)
        {
            MarkLeadersPercent = 0;

            int N = SetItems.Count;
            if (N == 0)
                return;

            UnitsLeaderToTranslate.Clear();
            UnitsToTranslate.Clear();

            int MaxCharsForLeaderSelection = EngineConfig.Config.ContextLimit;

            var FilteredItems = new List<int>();
            var LeaderIndexSet = new HashSet<int>();

            for (int i = 0; i < N; i++)
            {
                var item = SetItems[i];
                item.TempSim = 0;

                if (!string.IsNullOrEmpty(item.SourceText) &&
                    item.SourceText.Length > MaxCharsForLeaderSelection)
                {
                    UnitsToTranslate.Add(item);
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

            foreach (var idx in FilteredItems)
            {
                TokensCache[idx] = TextTokenizer
                    .Tokenize(Lang, SetItems[idx].SourceText)
                    .Where(t => t.Length >= 3)
                    .Select(t => t.ToLowerInvariant())
                    .Take(10)
                    .ToHashSet();
            }

            var PrefixBuckets = new Dictionary<string, List<int>>();

            foreach (var idx in FilteredItems)
            {
                var prefix = BuildPrefixKey(SetItems[idx].SourceText, 3);

                if (!PrefixBuckets.TryGetValue(prefix, out var list))
                {
                    list = new List<int>();
                    PrefixBuckets[prefix] = list;
                }

                list.Add(idx);
            }

            int ProcessedCount = UnitsToTranslate.Count;
            int TotalToProcess = N;
            int UpdateInterval = Math.Max(1, TotalToProcess / 100);

            foreach (var bucket in PrefixBuckets.Values)
            {
                if (bucket.Count == 0)
                    continue;

                int leaderIdx = PickContextLeader(bucket, SetItems, TokensCache);
                LeaderIndexSet.Add(leaderIdx);

                var leaderItem = SetItems[leaderIdx];
                leaderItem.TempSim = bucket.Count - 1;

                if (!string.IsNullOrEmpty(leaderItem.Key))
                {
                    UnitsLeaderToTranslate[leaderItem.Key] = leaderItem;
                }
                else
                {
                    UnitsToTranslate.Add(leaderItem);
                }

                ProcessedCount++;

                foreach (var idx in bucket)
                {
                    if (idx == leaderIdx)
                        continue;

                    UnitsToTranslate.Add(SetItems[idx]);
                    ProcessedCount++;
                }

                if (ProcessedCount % UpdateInterval == 0)
                {
                    MarkLeadersPercent = Math.Round(
                        Math.Min(ProcessedCount, TotalToProcess) * 100.0 / TotalToProcess, 2);
                }
            }

            var SecondStageMap = new Dictionary<string, int>();
            var RemoveLeaders = new List<string>();

            foreach (var kv in UnitsLeaderToTranslate)
            {
                var item = kv.Value;
                var key2 = BuildPrefixKey(item.SourceText, 2);

                if (SecondStageMap.ContainsKey(key2))
                {
                    UnitsToTranslate.Add(item);
                    RemoveLeaders.Add(kv.Key);
                }
                else
                {
                    SecondStageMap[key2] = 1;
                }
            }

            foreach (var k in RemoveLeaders)
                UnitsLeaderToTranslate.Remove(k);

            MarkLeadersPercent = 100;
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

            if (EngineConfig.Config.MaxThreadCount <= 0)
            {
                EngineConfig.Config.MaxThreadCount = 1;
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
            }

            for (int i = 0; i < this.UnitsToTranslate.Count; i++)
            {
                this.UnitsToTranslate[i].Translated = false;
                this.UnitsToTranslate[i].WorkEnd = 0;
                this.UnitsToTranslate[i].TransText = string.Empty;
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
                                ThreadUsage.MaxThreads = EngineConfig.Config.MaxThreadCount;

                                bool CanExit = true;
                                Token.ThrowIfCancellationRequested();
                                CurrentTrds = GetWorkCount();
                                
                                int AutoTrd = EngineConfig.Config.MaxThreadCount;

                                if (IsLeader)
                                {
                                    AutoTrd = 1;
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

                                    if (CurrentTrds > EngineConfig.Config.MaxThreadCount * EngineConfig.Config.ThrottleRatio)
                                    {
                                        AutoSleep = EngineConfig.Config.ThrottleDelayMs;
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
                }
            }

            foreach (var kvp in UnitsLeaderToTranslate)
            {
                if (kvp.Value.Transing)
                {
                    try
                    {
                        kvp.Value.CancelWorkThread();
                    }
                    catch { }
                }
            }

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
