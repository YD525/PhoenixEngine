
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
    // Copyright (c) 2025 YD525
    // Licensed under the MIT License.
    // See LICENSE file in the project root for full license information.
    //https://github.com/YD525/PhoenixEngine

    public class TranslationUnit
    {
        public int FileUniqueKey = 0;
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

        public bool CanTrans(int State)
        {
            if (DelegateHelper.SetTranslationUnitCallBack != null)
            {
                return DelegateHelper.SetTranslationUnitCallBack(this,State);
            }

            return true;
        }

        public void StartWork(BatchTranslationCore Source)
        {
            if (!CanTrans(0))
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

                        if (!CanTrans(1))
                        {
                            WorkEnd = 2;
                            return;
                        }

                        var GetResult = Translator.QuickTrans(this, ref CanSleep);
                        if (GetResult.Trim().Length > 0)
                        {
                            TransText = GetResult.Trim();

                            if (!CanTrans(2))
                            {
                                WorkEnd = 2;
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

        public TranslationUnit(int FileUniqueKey, string Key, string Type, string SourceText, string TransText,string AIParam,Languages From,Languages To,double Score)
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

        public double MarkLeadersPercent = 0;

        /// <summary>
        /// High-performance leader marking with token-based similarity
        /// </summary>
        /// <param name="SetItems">List of translation units</param>
        /// <param name="Lang">Language for tokenization</param>
        /// <param name="SimilarityThreshold">Minimum similarity to group as leader</param>
        public void MarkLeadersAndSortHighPerfAccumulate(List<TranslationUnit> SetItems, Languages Lang)
        {
            MarkLeadersPercent = 0;
            int N = SetItems.Count;
            if (N == 0) return;

            UnitsLeaderToTranslate.Clear();
            UnitsToTranslate.Clear();

            // Initialize TempSim For All Items
            foreach (var Item in SetItems)
                Item.TempSim = 0;

            // Precompute Tokens For All Items
            var TokensCache = new string[N][];
            for (int I = 0; I < N; I++)
                TokensCache[I] = TextTokenizer.Tokenize(Lang, SetItems[I].SourceText)
                                              .Select(T => T.ToLowerInvariant())
                                              .Take(10)
                                              .ToArray();

            // Build Inverted Index For Fast Token Lookup
            var TokenIndex = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
            for (int I = 0; I < N; I++)
            {
                foreach (var Token in TokensCache[I])
                {
                    if (!TokenIndex.TryGetValue(Token, out var List))
                    {
                        List = new List<int>();
                        TokenIndex[Token] = List;
                    }
                    List.Add(I);
                }
            }

            // Compute Cumulative Similarity
            for (int I = 0; I < N; I++)
            {
                var TokenSetA = TokensCache[I].ToHashSet();
                var RelatedIndices = new HashSet<int>();
                foreach (var Token in TokenSetA)
                {
                    if (TokenIndex.TryGetValue(Token, out var Indices))
                        RelatedIndices.UnionWith(Indices);
                }

                foreach (var J in RelatedIndices)
                {
                    if (I == J) continue;

                    int Intersection = TokenSetA.Intersect(TokensCache[J]).Count();
                    int Union = TokenSetA.Union(TokensCache[J]).Count();
                    double Sim = Union > 0 ? (double)Intersection / Union : 0;

                    // Accumulate Similarity Score
                    SetItems[I].TempSim += Sim;
                }

                MarkLeadersPercent = Math.Round(((double)(I + 1) * 100 / N), 2);
            }

            // Sort By TempSim Descending
            UnitsLeaderToTranslate.AddRange(SetItems.OrderByDescending(X => X.TempSim));

            // Move Items With TempSim == 0 To UnitsToTranslate
            var ToMove = UnitsLeaderToTranslate.Where(X => X.TempSim == 0).ToList();
            foreach (var Item in ToMove)
            {
                UnitsLeaderToTranslate.Remove(Item);
                UnitsToTranslate.Add(Item);
            }

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

                    if (!SkipWordAnalysis)
                    {
                        MarkLeadersAndSortHighPerfAccumulate(new List<TranslationUnit>(this.UnitsToTranslate), this.DetectSourceLang);
                    }

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
