using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using PhoenixEngine.ADO;
using PhoenixEngine.Engine;
using PhoenixEngine.Events;
using PhoenixEngine.PThread;
using PhoenixEngine.Sequence;
using PhoenixEngine.Unit;

namespace PhoenixEngine.Translate
{
    public class TranslatorCore
    {
        public ProcContent Content = null;

        public ConcurrentQueue<BaseUnit> TranslatedQueue = new ConcurrentQueue<BaseUnit>();

        public volatile int AutoThreadLimit = 0;

        public double MarkLeadersPercent = 0;

        public volatile bool SkipWordAnalysis = false;

        public Translator TranslatorRef = null;

        public P_ThreadPool<UnitGroup> TrdPool = null;

        public readonly object CacheSetGetLock = new object();
        public Dictionary<string, string> DequeueCache = new Dictionary<string, string>();

        public volatile int ProcStage = 0;

        public volatile bool IsStopped = false;
        public volatile bool IsWorking = false;

        private volatile bool UnitForDone = false;
        private volatile bool BookForDone = false;

        public volatile int BaseTranslatedCount = 0;
        public volatile int TranslatedCount = 0;

        public TranslatorCore(Translator SetTranslator, bool ClearCache = false)
        {
            ProcStage = 0;
            this.TranslatorRef = SetTranslator;

            if (ClearCache)
            {
                TranslatorRef.ClearCache();
            }
        }

        public int GetWorkingThreadCount()
        {
            if (TrdPool != null)
            {
                return TrdPool.GetCount();
            }
            return 0;
        }

        public int GetCount()
        {
            return Content.GetCount();
        }

        public bool Init(List<BaseUnit> BaseUnits, AggregationMode SetMode, int Addition)
        {
            if (ProcStage == 0)
            {
                this.Close();
                this.TranslatorRef.SyncTranslatedCount(Addition);
                UnionArray SetData = new UnionArray();
                ProcStage = 1;
                SetData.Load(BaseUnits, TranslatorRef.From, ref MarkLeadersPercent);
                Content = ProcContent.Build(TranslatorRef, SetData, SetMode);

                if (SetMode == AggregationMode.Aggregation)
                {
                    ProcContent.ArrangeForParallel(Content, Phoenix.Config.MaxThreadCount);
                }

                ProcStage = 2;

                if (Phoenix.Config.MaxThreadCount <= 0)
                {
                    Phoenix.Config.MaxThreadCount = 1;
                }

                return true;
            }

            return false;
        }

        public Thread TransMainTrd = null;

        private void WaitAllDone(CancellationToken Token, Func<bool> ForDoneCheck = null)
        {
            int EmptyConfirmCount = 0;

            while (true)
            {
                Token.ThrowIfCancellationRequested();

                bool ForDone = ForDoneCheck == null || ForDoneCheck();
                bool PoolEmpty = TrdPool.GetCount() == 0;
                bool QueueEmpty = TranslatedQueue.IsEmpty;

                if (ForDone && PoolEmpty && QueueEmpty)
                {
                    EmptyConfirmCount++;
                    if (EmptyConfirmCount >= 3)
                        break;
                }
                else
                {
                    EmptyConfirmCount = 0;
                }

                Thread.Sleep(100);
            }
        }

        public void Start()
        {
            _CancelSource?.Dispose();
            _CancelSource = new CancellationTokenSource();

            var MainTrdToken = _CancelSource.Token;

            int TrdDelayMs = Phoenix.Config.ThrottleDelayMs;

            BaseTranslatedCount = TranslatorRef.CalcTranslatedCount(0);

            if (TrdPool == null)
            {
                TrdPool = new P_ThreadPool<UnitGroup>();
                TrdPool.ConcurrencyLimit = Phoenix.Config.MaxThreadCount;
            }
            else
            {
                TrdPool.ConcurrencyLimit = Phoenix.Config.MaxThreadCount;
            }

            double ThrottleLimit = ((double)TrdPool.ConcurrencyLimit * (double)Phoenix.Config.ThrottleRatio);

            TransMainTrd = new Thread(() =>
            {
                try
                {
                    MainTrdToken.ThrowIfCancellationRequested();

                    this.IsWorking = true;
                    this.ProcStage = 3;
                    UnitForDone = false;
                    BookForDone = false;

                    for (int I = 0; I < this.Content.Units.Count; I++)
                    {
                        MainTrdToken.ThrowIfCancellationRequested();

                        UnitGroup GetUnit = this.Content.Units[I];

                        if (!GetUnit.ApplyStateChange(TranslatorRef.ID,UnitTranslationState.Created).CanDo(-1))
                            continue;

                        while (!TrdPool.Put(GetUnit, new Do_Thread<UnitGroup>(
                            new Action<UnitGroup, CancellationToken, ManualResetEventSlim>((UnitRef, Token, Pause) =>
                            {
                                Token.ThrowIfCancellationRequested();

                                Thread.Sleep(100);

                                Pause.Wait(Token);

                                Token.ThrowIfCancellationRequested();

                                if (UnitRef == null) return;
                                UnitRef = TranslatorRef.Translate(new TransParam(UnitRef, false, true),Token);

                                //Token.ThrowIfCancellationRequested();
                                if(UnitRef!=null)
                                AddTranslated(TranslatorRef.ID,UnitRef);
                            }), null)))
                        {
                            MainTrdToken.ThrowIfCancellationRequested();
                            if (GetCount() > ThrottleLimit)
                                Thread.Sleep(TrdDelayMs);
                        }
                    }

                    MainTrdToken.ThrowIfCancellationRequested();

                    UnitForDone = true;
                    WaitAllDone(MainTrdToken, () => UnitForDone);

                    MainTrdToken.ThrowIfCancellationRequested();

                    this.ProcStage = 5;

                    for (int I = 0; I < this.Content.Books.Count; I++)
                    {
                        MainTrdToken.ThrowIfCancellationRequested();

                        UnitGroup GetBook = this.Content.Books[I];

                        if (!GetBook.ApplyStateChange(TranslatorRef.ID,UnitTranslationState.Created).CanDo(-1))
                            continue;

                        while (!TrdPool.Put(GetBook, new Do_Thread<UnitGroup>(
                             new Action<UnitGroup, CancellationToken, ManualResetEventSlim>((BookRef, Token, Pause) =>
                             {
                                 Token.ThrowIfCancellationRequested();

                                 Thread.Sleep(100);

                                 Pause.Wait(Token);

                                 Token.ThrowIfCancellationRequested();

                                 if (BookRef == null) return;
                                 BookRef = TranslatorRef.Translate(new TransParam(BookRef, false, true),Token);

                                 //Token.ThrowIfCancellationRequested();
                                 if (BookRef != null)
                                 AddTranslated(TranslatorRef.ID, BookRef);
                             }), null)))
                        {
                            MainTrdToken.ThrowIfCancellationRequested();
                            if (GetCount() > ThrottleLimit)
                                Thread.Sleep(TrdDelayMs);
                        }
                    }

                    MainTrdToken.ThrowIfCancellationRequested();

                    BookForDone = true;
                    WaitAllDone(MainTrdToken, () => BookForDone);

                    MainTrdToken.ThrowIfCancellationRequested();

                    this.ProcStage = 6;

                    this.Content.SyncSameItemsFromTranslated();

                    for (int I = 0; I < this.Content.SameItems.Count; I++)
                    {
                        MainTrdToken.ThrowIfCancellationRequested();

                        for (int Ir = 0; Ir < this.Content.SameItems[I].Units.Count; Ir++)
                        {
                            MainTrdToken.ThrowIfCancellationRequested();

                            var GetUnit = this.Content.SameItems[I].Units[Ir];
                            var Link = TranslatorRef.GetLink();

                            if (GetUnit.Translated.Length > 0)
                            {
                                CloudDBCache.AddCache(
                                    TranslatorRef.GetFileUniqueKey(),
                                    GetUnit.Key,
                                    (int)TranslatorRef.To,
                                    GetUnit.Original,
                                    GetUnit.Translated
                                );
                                TranslatedQueue.Enqueue(GetUnit);

                                Link[GetUnit.Key] = GetUnit.Translated;

                                continue;
                            }

                            bool GetState = false;
                            string CacheResult = "";

                            lock (CacheSetGetLock)
                            {
                                if (DequeueCache.TryGetValue(GetUnit.Original, out var TempCacheResult))
                                {
                                    GetState = true;
                                    CacheResult = TempCacheResult;
                                }
                            }


                            if (GetState)
                            {
                                GetUnit.Translated = CacheResult;

                                CloudDBCache.AddCache(
                                    TranslatorRef.GetFileUniqueKey(),
                                    GetUnit.Key,
                                    (int)TranslatorRef.To,
                                    GetUnit.Original,
                                    GetUnit.Translated
                                );
                                TranslatedQueue.Enqueue(GetUnit);

                                Link[GetUnit.Key] = CacheResult;
                            }
                        }
                    }

                    MainTrdToken.ThrowIfCancellationRequested();

                    WaitAllDone(MainTrdToken);
                }
                catch (OperationCanceledException) { }
                catch (Exception) { }
                finally
                {
                    lock (CacheSetGetLock)
                        DequeueCache.Clear();

                    this.IsWorking = false;
                    this.ProcStage = 10;
                    TrdPool?.Dispose();
                    TrdPool = null;
                    TransMainTrd = null;
                }
            });

            TransMainTrd.Start();
        }

        private CancellationTokenSource _CancelSource;

        public void Keep()
        {
            IsStopped = false;
            TrdPool.SuspendAll(false);
        }

        public void Stop()
        {
            IsStopped = true;
            TrdPool.SuspendAll(true);
        }

        private void AddTranslated(string TranslatorID,UnitGroup Item)
        {
            if (!Item.ApplyStateChange(TranslatorID,UnitTranslationState.Queued).CanDo(-1))
                return;

            for (int I = 0; I < Item.Units.Count; I++)
            {
                TranslatedQueue.Enqueue(Item.Units[I]);
            }
        }

        public BaseUnit DequeueTranslated(out bool IsEnd)
        {
            try
            {
                if (TranslatedQueue.Count > 0)
                {
                    var State = TranslatedQueue.TryDequeue(out BaseUnit Item);

                    IsEnd = false;

                    if (State)
                    {
                        Interlocked.Increment(ref TranslatedCount);

                        lock (CacheSetGetLock)
                            DequeueCache[Item.GetRealOriginal()] = Item.Translated;

                        return Item;
                    }
                    else
                    {
                        return null;
                    }
                }

                if (this.ProcStage == 10 && GetWorkingThreadCount() == 0)
                    IsEnd = true;
                else
                    IsEnd = false;

                return null;
            }
            catch
            {
                IsEnd = false;
                return null;
            }
        }
        public void Close()
        {
            lock (CacheSetGetLock)
                DequeueCache.Clear();

            ProcStage = 0;
            IsStopped = false;
            UnitForDone = false;
            BookForDone = false;
            IsWorking = false;
            TrdPool?.Dispose();
            TrdPool = null;
            _CancelSource?.Cancel();

            while (TranslatedQueue.TryDequeue(out _)) { }

            MarkLeadersPercent = 0;

            this.Content?.Clear();

            Interlocked.Exchange(ref TranslatedCount, 0);
            BaseTranslatedCount = 0;
        }
    }
}