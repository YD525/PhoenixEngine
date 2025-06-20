
using PhoenixEngine.DelegateManagement;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManagement;

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
        public Thread ?CurrentTrd;
        public double Score = 100;
        public string Key = "";
        public string Type = "";
        public string SourceText = "";
        public string TransText = "";
        public bool IsDuplicateSource = false;
        public bool Transing = false;
        public bool Leader = false;
        public bool Translated = false;
        public Languages SourceLanguage = Languages.Auto;
        public Languages TargetLanguage = Languages.Null;

        private CancellationTokenSource ?TransThreadToken;

        public void StartWork()
        {
            if (this.TransText.Trim().Length > 0)
            {
                WorkEnd = 2;
                return;
            }

            if (this.IsDuplicateSource)
            {
                if (!BatchTranslationHelper.SameItems.ContainsKey(this.SourceText))
                {
                    BatchTranslationHelper.SameItems.Add(this.SourceText, string.Empty);
                }
                else
                {
                    this.Transing = false;
                    WorkEnd = 2;
                    return;
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
                                DelegateHelper.SetLog("Skip Book fields:" + this.Key,0);
                            }

                            WorkEnd = 2;
                        }
                        else
                        if (this.Score < 5)
                        {
                            if (DelegateHelper.SetLog != null)
                            {
                                DelegateHelper.SetLog("Skip dangerous fields:" + this.Key,0);
                            }
                            WorkEnd = 2;
                        }

                        if (WorkEnd != 2)
                        {
                            bool CanSleep = true;
                            var GetResult = Translator.QuickTrans(this.ModName, this.Type, this.Key, this.SourceText, this.SourceLanguage, this.TargetLanguage, ref CanSleep);
                            if (GetResult.Trim().Length > 0)
                            {
                                TransText = GetResult.Trim();

                                if (Translator.TransData.ContainsKey(this.Key))
                                {
                                    Translator.TransData[this.Key] = GetResult;
                                }
                                else
                                {
                                    Translator.TransData.Add(this.Key, GetResult);
                                }

                                if (this.IsDuplicateSource)
                                {
                                    if (BatchTranslationHelper.SameItems.ContainsKey(this.SourceText))
                                    {
                                        BatchTranslationHelper.SameItems[this.SourceText] = GetResult;
                                    }
                                }

                                WorkEnd = 2;

                                this.Translated = true;

                                BatchTranslationHelper.AddTranslatedByKey(this.Key);

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

        public TranslationUnit(string ModName,string Key, string Type, string SourceText, string TransText)
        {
            this.ModName = ModName;
            this.Key = Key;
            this.Type = Type;
            this.SourceText = SourceText;
            this.TransText = TransText;
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

        public static List<TranslationUnit> MarkLeadersAndSortWithTokenSimilarity(List<TranslationUnit> Items, Languages Lang)
        {
            int N = Items.Count;
            double[,] SimMatrix = new double[N, N];

            // Calculate similarity matrix
            for (int I = 0; I < N; I++)
            {
                for (int J = I; J < N; J++)
                {
                    double Sim = TokenBasedSimilarity(Items[I].SourceText, Items[J].SourceText, Lang);
                    SimMatrix[I, J] = Sim;
                    SimMatrix[J, I] = Sim;
                }
            }

            // Calculate similarity sums for each item
            double[] SimSums = new double[N];
            for (int I = 0; I < N; I++)
            {
                double Sum = 0;
                for (int J = 0; J < N; J++)
                {
                    if (I != J) Sum += SimMatrix[I, J];
                }
                SimSums[I] = Sum;
            }

            // Find the item with the highest similarity sum as leader
            double MaxSimSum = SimSums.Max();
            int LeaderIndex = Array.IndexOf(SimSums, MaxSimSum);

            // Reset all leader flags
            foreach (var Item in Items)
                Item.Leader = false;

            if (LeaderIndex >= 0 && LeaderIndex < N)
                Items[LeaderIndex].Leader = true;

            // Sort: leaders first, then by Key
            return Items.OrderByDescending(x => x.Leader).ThenBy(x => x.Key).ToList();
        }
    }
    public class BatchTranslationHelper
    {
        public static Dictionary<string, string> SameItems = new Dictionary<string, string>();
        public static List<TranslationUnit> TranslationUnits = new List<TranslationUnit>();
        public static Queue<string> TranslatedKeys = new Queue<string>();

        public static ThreadUsageInfo ThreadUsage = new ThreadUsageInfo();

        public static object TranslatedAddLocker = new object();
        public static void AddTranslatedByKey(string Key)
        {
            lock (TranslatedAddLocker)
            {
                TranslatedKeys.Enqueue(Key);
            }
        }

        public static int GetWorkCount()
        {
            int WorkCount = 0;
            for (int i = 0; i < TranslationUnits.Count; i++)
            {
                if (TranslationUnits[i].Transing)
                {
                    WorkCount++;
                }
            }
            return WorkCount;
        }
        public static void MarkDuplicates(List<TranslationUnit> Items)
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
        public static void Init()
        {
            TranslatedKeys.Clear();
            SameItems.Clear();
            TranslationUnits.Clear();

            InitTranslationUnits();

            MarkDuplicates(TranslationUnits);

            if (EngineConfig.MaxThreadCount <= 0)
            {
                EngineConfig.MaxThreadCount = 1;
            }

            AutoSleep = 1;
        }

        public static void InitTranslationUnits()
        { 
        
        }

        public static CancellationTokenSource TransMainTrdCancel = null;
        public static Thread ?TransMainTrd = null;

        public static int CurrentTrdCount = 0;

        public static void CancelMainTransThread()
        {
            TransMainTrdCancel?.Cancel();
        }
        public static int AutoSleep = 1;

        public static bool IsWork = false;

        public static int WorkState = 0;
        public static void Start()
        {
            WorkState = 0;

            Init();

            TransMainTrd = new Thread(() =>
            {
                IsWork = true;
                TransMainTrdCancel = new CancellationTokenSource();
                var Token = TransMainTrdCancel.Token;

                int CurrentTrds = 0;
                while (true)
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
                            for (int i = 0; i < TranslationUnits.Count; i++)
                            {
                                if (TranslationUnits[i].WorkEnd <= 0)
                                {
                                    TranslationUnits[i].StartWork();
                                    CanExit = false;
                                    break;
                                }
                            }

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
                            for (int i = 0; i < TranslationUnits.Count; i++)
                            {
                                if (TranslationUnits[i].WorkEnd == 2)
                                {
                                    SucessCount++;
                                }
                            }
                            if (SucessCount == TranslationUnits.Count)
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

                                WorkState = 1;

                                BatchTranslationHelper.Close();
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

                    Thread.Sleep(1);
                }

            });

            TransMainTrd.Start();
        }
        public static void Close()
        {
            try
            {
                CancelMainTransThread();
            }
            catch { }

            for (int i = 0; i < TranslationUnits.Count; i++)
            {
                if (TranslationUnits[i].Transing)
                {
                    try
                    {
                        TranslationUnits[i].CancelWorkThread();
                    }
                    catch { }
                }
            }

            TransMainTrd = null;
        }

        public static void SetDuplicateSource(string GetKey)
        {
            for (int ir = 0; ir < TranslationUnits.Count; ir++)
            {
                if (TranslationUnits[ir].SourceText.Equals(GetKey))
                {
                    if (Translator.TransData.ContainsKey(TranslationUnits[ir].Key))
                    {
                        Translator.TransData[TranslationUnits[ir].Key] = SameItems[GetKey];
                    }
                    else
                    {
                        Translator.TransData.Add(TranslationUnits[ir].Key, SameItems[GetKey]);
                    }
                }
            }
        }

    }
}
