
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.VisualBasic;
using PhoenixEngine.DelegateManagement;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManagement;
using static PhoenixEngine.SSELexiconBridge.NativeBridge;
using static PhoenixEngine.TranslateCore.LanguageHelper;

namespace PhoenixEngine.TranslateManage
{
    // Copyright (C) 2025 YD525
    // Licensed under the GNU GPLv3
    // See LICENSE for details
    //https://github.com/YD525/PhoenixEngine

    public class TranslationUnit
    {
        public string ModName = "";
        public int WorkEnd = 0;
        public Thread? CurrentTrd;
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

        private CancellationTokenSource? TransThreadToken;

        public bool CanTrans()
        {
            if (DelegateHelper.SetTranslationUnitCallBack != null)
            {
                return DelegateHelper.SetTranslationUnitCallBack(this);
            }

            return true;
        }

        public void StartWork(BatchTranslationCore Source)
        {
            if (!CanTrans())
            {
                WorkEnd = 2;
                return;
            }

            if (this.TransText.Trim().Length > 0)
            {
                WorkEnd = 2;
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
                        WorkEnd = 2;
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
                        bool CanAddCache = true;

                        if (!CanTrans())
                        {
                            WorkEnd = 2;
                            return;
                        }

                        var GetResult = Translator.QuickTrans(this, ref CanSleep, ref CanAddCache);
                        if (GetResult.Trim().Length > 0)
                        {
                            if (!CanTrans())
                            {
                                WorkEnd = 2;
                                return;
                            }

                            TransText = GetResult.Trim();

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

        public TranslationUnit(string ModName, string Key, string Type, string SourceText, string TransText,string AIParam,Languages From,Languages To)
        {
            this.ModName = ModName;
            this.Key = Key;
            this.Type = Type;
            this.SourceText = SourceText;
            this.TransText = TransText;
            this.AIParam = AIParam;
            this.From = From;
            this.To = To;
        }
    }
    public class BatchTranslationCore
    {
        public readonly object SameItemsLocker = new object();

        public Dictionary<string, string> SameItems = new Dictionary<string, string>();

        public List<TranslationUnit> UnitsLeaderToTranslate = new List<TranslationUnit>();

        public List<TranslationUnit> UnitsToTranslate = new List<TranslationUnit>();

        public readonly object UnitsTranslatedLocker = new object();

        public Queue<TranslationUnit> UnitsTranslated = new Queue<TranslationUnit>();

        public List<string> TranslatedKeys = new List<string>();

        public int AutoThreadLimit = 0;

        public Languages DetectSourceLang = Languages.Null;

        public Languages From = Languages.Auto;
        public Languages To = Languages.Null;

        public bool IsStop = false;

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

        public static double TokenBasedSimilarityR(string TextA, string TextB, Languages Lang, int MaxTokens = 3)
        {
            // Tokenize and limit number of tokens
            var TokensA = TextTokenizer.Tokenize(Lang, TextA)
                                       .Select(t => t.ToLowerInvariant())
                                       .Take(MaxTokens)
                                       .ToHashSet();

            var TokensB = TextTokenizer.Tokenize(Lang, TextB)
                                       .Select(t => t.ToLowerInvariant())
                                       .Take(MaxTokens)
                                       .ToHashSet();

            if (TokensA.Count == 0 && TokensB.Count == 0) return 1.0;
            if (TokensA.Count == 0 || TokensB.Count == 0) return 0.0;

            var Intersection = TokensA.Intersect(TokensB).Count();
            var Union = TokensA.Union(TokensB).Count();

            return (double)Intersection / Union; // Jaccard similarity
        }

        public int MarkLeadersPercent = 0;
        public void MarkLeadersAndSortWithTokenSimilarityAndSeparate(
     List<TranslationUnit> SetItems, Languages Lang, int MaxLength = 200, double SimilarityThreshold = 0.2)
        {
            MarkLeadersPercent = 0;

            List<TranslationUnit> Items = new List<TranslationUnit>();
            Items.AddRange(SetItems);

            if (Items == null || Items.Count == 0) return;

            // Clear previous lists
            UnitsLeaderToTranslate.Clear();
            UnitsToTranslate.Clear();

            int N = Items.Count;
            bool[] IsLeader = new bool[N];
            bool[] IsCandidate = new bool[N];

            // Determine candidate sentences (length filter)
            for (int i = 0; i < N; i++)
                IsCandidate[i] = (Items[i]?.SourceText?.Length ?? 0) <= MaxLength;

            int NN = Items.Count;
            int UpdateInterval = Math.Max(1, NN / 100);
            int Processed = 0;

            // For each candidate, find items that are most similar
            for (int i = 0; i < N; i++)
            {
                if (!IsCandidate[i] || IsLeader[i])
                {
                    Processed++;
                    if (Processed % UpdateInterval == 0 || Processed == NN)
                        MarkLeadersPercent = (int)(Processed * 100.0 / NN);
                    continue;
                }

                if (ExitAny) return;

                // Start a new leader group
                List<(int Index, double Sim)> LeaderGroup = new List<(int, double)> { (i, 1.0) }; // self sim = 1

                for (int j = 0; j < N; j++)
                {
                    if (i == j || !IsCandidate[j] || IsLeader[j]) continue;

                    double sim = TokenBasedSimilarityR(Items[i].SourceText, Items[j].SourceText, Lang);
                    if (sim >= SimilarityThreshold)
                        LeaderGroup.Add((j, sim));
                }

                // Mark all in the group as leader and store sim in TranslationUnit
                foreach (var (idx, sim) in LeaderGroup)
                {
                    IsLeader[idx] = true;
                    Items[idx].TempSim = sim;
                }
            }

            // Fill UnitsLeaderToTranslate and UnitsToTranslate
            for (int i = 0; i < N; i++)
            {
                if (IsLeader[i])
                    UnitsLeaderToTranslate.Add(Items[i]);
                else
                    UnitsToTranslate.Add(Items[i]);
            }

            // Sort leaders by TempSim descending
            UnitsLeaderToTranslate.Sort((a, b) => b.TempSim.CompareTo(a.TempSim));

            GC.Collect();
        }

        public ThreadUsageInfo ThreadUsage = new ThreadUsageInfo();

        public readonly object TranslatedAddLocker = new object();

        public void AddTranslated(TranslationUnit Item)
        {
            lock (TranslatedAddLocker)
            {
                UnitsTranslated.Enqueue(Item);
                TranslatorBridge.SetCloudTransData(Item.Key,Item.SourceText,Item.TransText);
                TranslatedKeys.Add(Item.Key);
            }
        }

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

            for (int i = 0; i < UnitsLeaderToTranslate.Count; i++)
            {
                if (UnitsLeaderToTranslate[i].Transing)
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

            if (EngineConfig.MaxThreadCount <= 0)
            {
                EngineConfig.MaxThreadCount = 1;
            }

            AutoSleep = 1;
        }

        public CancellationTokenSource TransMainTrdCancel = null;
        public Thread? TransMainTrd = null;

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
        public void Start()
        {
            if (IsWork || TransMainTrd == null)
            {
                ExitAny = false;
                TransMainTrd = new Thread(() =>
                {
                    IsWork = true;

                    WorkState = 1;

                    if (this.From != Languages.Auto)
                    {
                        this.DetectSourceLang = this.From;
                    }
                    else
                    {
                        FileLanguageDetect? LangDetecter = new FileLanguageDetect();

                        for (int i = 0; i < this.UnitsToTranslate.Count; i++)
                        {
                            LangDetecter.DetectLanguageByFile(this.UnitsToTranslate[i].SourceText);
                        }

                        this.DetectSourceLang = LangDetecter.GetLang();

                        LangDetecter = null;
                    }

                    MarkLeadersAndSortWithTokenSimilarityAndSeparate(this.UnitsToTranslate, this.DetectSourceLang);

                    if (ExitAny)
                    {
                        SetEndState();
                        return;
                    }

                    TransMainTrdCancel = new CancellationTokenSource();
                    var Token = TransMainTrdCancel.Token;

                    int CurrentTrds = 0;

                    WorkState = 2;

                    while (true)
                    {
                        if (!IsStop)
                        {
                            try
                            {
                                NextFind:

                                ThreadUsage.CurrentThreads = CurrentTrds;
                                ThreadUsage.MaxThreads = EngineConfig.MaxThreadCount;

                                bool CanExit = true;
                                Token.ThrowIfCancellationRequested();
                                CurrentTrds = GetWorkCount();

                                if (CurrentTrds < EngineConfig.MaxThreadCount)
                                {
                                    var Leader = UnitsLeaderToTranslate.FirstOrDefault(u => u.WorkEnd <= 0);
                                    if (Leader != null)
                                    {
                                        Leader.StartWork(this);
                                        CanExit = false;
                                        goto Next;
                                    }

                                    var Normal = UnitsToTranslate.FirstOrDefault(u => u.WorkEnd <= 0);
                                    if (Normal != null)
                                    {
                                        Normal.StartWork(this);
                                        CanExit = false;
                                        goto Next;
                                    }

                                    Next:

                                    if (CurrentTrds > EngineConfig.MaxThreadCount * EngineConfig.ThrottleRatio)
                                    {
                                        AutoSleep = EngineConfig.ThrottleDelayMs;
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

                                    for (int i = 0; i < UnitsLeaderToTranslate.Count; i++)
                                    {
                                        if (UnitsLeaderToTranslate[i].WorkEnd == 2)
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
            IEnumerable<TranslationUnit> AllUnits = UnitsToTranslate.Concat(UnitsLeaderToTranslate);

            foreach (var Unit in AllUnits)
            {
                if (Unit.SourceText == Source && !TranslatedKeys.Contains(Unit.Key))
                {
                    lock (Translator.TransDataLocker)
                    {
                        Translator.TransData[Unit.Key] = SameItems[Source];
                    }

                    lock (TranslatedAddLocker)
                    {
                        UnitsTranslated.Enqueue(Unit);
                        TranslatedKeys.Add(Unit.Key);
                    }
                }
            }
        }

        public TranslationUnit? DequeueTranslated(out bool IsEnd)
        {
            lock (UnitsTranslatedLocker)
            {
                if (UnitsTranslated.Count == 0)
                {
                    if (this.WorkState == 3 && GetWorkCount() == 0)
                    {
                        IsEnd = true;
                        return null;
                    }
                    else
                    {
                        IsEnd = false;
                        return null;
                    }
                }

                IsEnd = false;

                var GetResult = UnitsTranslated.Dequeue();

                if (GetResult.TransText.Trim().Length > 0)
                {
                    return GetResult;
                }
                else
                {
                    return null;
                }
            }
        }
    }
}
