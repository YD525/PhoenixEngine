
using System.Linq;
using PhoenixEngine.DelegateManagement;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManagement;
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

        private CancellationTokenSource? TransThreadToken;

        public void StartWork(BatchTranslationCore Source)
        {
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
                        if (this.Type.Equals("Book") && (!Key.EndsWith("(Description)") && !Key.EndsWith("(Name)")))
                        {
                            if (DelegateHelper.SetLog != null)
                            {
                                DelegateHelper.SetLog("Skip Book fields:" + this.Key, 0);
                            }

                            WorkEnd = 2;
                        }
                        else
                        if (this.Score < 5)
                        {
                            if (DelegateHelper.SetLog != null)
                            {
                                DelegateHelper.SetLog("Skip dangerous fields:" + this.Key, 0);
                            }
                            WorkEnd = 2;
                        }

                        if (WorkEnd != 2)
                        {
                            bool CanSleep = true;
                            var GetResult = Translator.QuickTrans(this.ModName, this.Type, this.Key, this.SourceText, Source.From, Source.To, ref CanSleep);
                            if (GetResult.Trim().Length > 0)
                            {
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
                                goto NextGet;
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

        public TranslationUnit(string ModName, string Key, string Type, string SourceText, string TransText)
        {
            this.ModName = ModName;
            this.Key = Key;
            this.Type = Type;
            this.SourceText = SourceText;
            this.TransText = TransText;
        }
    }
    public class BatchTranslationCore
    {
        public readonly object SameItemsLocker = new object();

        public Dictionary<string, string> SameItems = new Dictionary<string, string>();

        private List<TranslationUnit> UnitsLeaderToTranslate = new List<TranslationUnit>();

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

        public static double TokenBasedSimilarity(string TextA, string TextB, Languages Lang)
        {
            // Tokenize
            var TokensA = TextTokenizer.Tokenize(Lang, TextA).Select(t => t.ToLowerInvariant()).ToHashSet();
            var TokensB = TextTokenizer.Tokenize(Lang, TextB).Select(t => t.ToLowerInvariant()).ToHashSet();

            if (TokensA.Count == 0 && TokensB.Count == 0) return 1.0;
            if (TokensA.Count == 0 || TokensB.Count == 0) return 0.0;

            var Intersection = TokensA.Intersect(TokensB).Count();
            var Union = TokensA.Union(TokensB).Count();

            return (double)Intersection / Union; // Jaccard similarity
        }

        public void MarkLeadersAndSortWithTokenSimilarityAndSeparate(List<TranslationUnit> Items, Languages Lang)
        {
            Int32 N = Items.Count;
            Double[,] SimMatrix = new Double[N, N];

            // Calculate token-based similarity matrix
            for (Int32 I = 0; I < N; I++)
            {
                for (Int32 J = I; J < N; J++)
                {
                    Double Sim = TokenBasedSimilarity(Items[I].SourceText, Items[J].SourceText, Lang);
                    SimMatrix[I, J] = Sim;
                    SimMatrix[J, I] = Sim;
                }
            }

            // Compute the sum of similarities for each item
            Double[] SimSums = new Double[N];
            for (Int32 I = 0; I < N; I++)
            {
                Double Sum = 0;
                for (Int32 J = 0; J < N; J++)
                {
                    if (I != J) Sum += SimMatrix[I, J];
                }
                SimSums[I] = Sum;
            }

            // Identify the leader with the highest total similarity
            Double MaxSimSum = SimSums.Max();
            Int32 LeaderIndex = Array.IndexOf(SimSums, MaxSimSum);

            // Reset all leader flags
            foreach (var Item in Items)
                Item.Leader = false;

            // Clear target lists
            UnitsLeaderToTranslate.Clear();
            UnitsToTranslate.Clear();

            // Assign the leader to its dedicated list
            if (LeaderIndex >= 0 && LeaderIndex < N)
            {
                Items[LeaderIndex].Leader = true;
                UnitsLeaderToTranslate.Add(Items[LeaderIndex]);
            }

            // Add non-leader items to the main list, sorted by Key
            var NonLeaders = Items.Where(X => !X.Leader).OrderBy(X => X.Key).ToList();
            UnitsToTranslate.AddRange(NonLeaders);
        }

        public ThreadUsageInfo ThreadUsage = new ThreadUsageInfo();

        public readonly object TranslatedAddLocker = new object();

        public void AddTranslated(TranslationUnit Item)
        {
            lock (TranslatedAddLocker)
            {
                UnitsTranslated.Enqueue(Item);
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

        public int CurrentTrdCount = 0;

        public void CancelMainTransThread()
        {
            TransMainTrdCancel?.Cancel();
        }
        public int AutoSleep = 1;

        public bool IsWork = false;

        public int WorkState = 0;

        public void Start()
        {
            if (IsStop)
            {
                IsStop = false;
                return;
            }

            if (IsWork || TransMainTrd != null)
            {
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

                                    if (SucessCount == UnitsToTranslate.Count)
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

                        Thread.Sleep(1);
                    }

                });

                TransMainTrd.Start();
            }
        }

        public void Close()
        {
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
                    if (this.WorkState == 3)
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
                return UnitsTranslated.Dequeue();
            }
        }
    }
}
