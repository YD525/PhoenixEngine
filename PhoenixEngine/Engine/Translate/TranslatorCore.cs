using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using PhoenixEngine.ADO;
using PhoenixEngine.EngineManagement.Engine;
using PhoenixEngine.Events;
using PhoenixEngine.PThread;
using PhoenixEngine.Sequence;
using PhoenixEngine.Unit;

namespace PhoenixEngine.Translate
{
    public class TranslatorCore
    {
        public readonly object UnitsReadLock = new object();

        public ProcContent Content = null;

        public ConcurrentQueue<BaseUnit> TranslatedQueue = new ConcurrentQueue<BaseUnit>();

        public int AutoThreadLimit = 0;

        public bool IsStop = false;

        public bool SkipWordAnalysis = false;

        public Translator TranslatorRef = null;

        public P_ThreadPool<UnitGroup> TrdPool = null;

        public Dictionary<string, string> DequeueCache = new Dictionary<string, string>();

        public int ProcStage = 0;

        public bool IsWork = false;

        private volatile bool UnitForDone = false;
        private volatile bool BookForDone = false;

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

        public readonly object TranslatedAddLocker = new object();

        public object WaitTranslateLock = new object();

        public double MarkLeadersPercent = 0;

        public int GetCount()
        {
            return Content.GetCount();
        }

        public bool Init(List<BaseUnit> BaseUnits, AggregationMode SetMode,int Addition)
        {
            if (ProcStage == 0)
            {
                Clear();
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

        private void WaitAllDone(Func<bool> ForDoneCheck = null)
        {
            int EmptyConfirmCount = 0;

            while (true)
            {
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
            int TrdDelayMs = Phoenix.Config.ThrottleDelayMs;

            TranslatedCount = TranslatorRef.CalcTranslatedCount(0);

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
                this.IsWork = true;
                this.ProcStage = 3;
                UnitForDone = false;
                BookForDone = false;

                for (int I = 0; I < this.Content.Units.Count; I++)
                {
                    UnitGroup GetUnit = this.Content.Units[I];

                    if (!GetUnit.ApplyStateChange(UnitTranslationState.Created).CanDo(-1))
                        continue;

                    while (!TrdPool.Put(GetUnit,new Do_Thread<UnitGroup>(
                        new Action<UnitGroup, CancellationToken, ManualResetEventSlim>((UnitRef,Token,Pause) =>
                        {
                            Token.ThrowIfCancellationRequested();

                            Thread.Sleep(100);

                            Pause.Wait(Token);

                            Token.ThrowIfCancellationRequested();

                            UnitRef = TranslatorRef.Translate(new TransParam(UnitRef, false, true));

                            Token.ThrowIfCancellationRequested();

                            AddTranslated(UnitRef);
                        }),null)))
                    {
                        if (GetCount() > ThrottleLimit)
                            Thread.Sleep(TrdDelayMs);
                    }
                }

                UnitForDone = true;
                WaitAllDone(() => UnitForDone);

                this.ProcStage = 5;

                for (int I = 0; I < this.Content.Books.Count; I++)
                {
                    UnitGroup GetBook = this.Content.Books[I];

                    if (!GetBook.ApplyStateChange(UnitTranslationState.Created).CanDo(-1))
                        continue;

                    while (!TrdPool.Put(GetBook, new Do_Thread<UnitGroup>(
                         new Action<UnitGroup, CancellationToken, ManualResetEventSlim>((BookRef, Token, Pause) =>
                         {
                             Token.ThrowIfCancellationRequested();

                             Thread.Sleep(100);

                             Pause.Wait(Token);

                             Token.ThrowIfCancellationRequested();

                             BookRef = TranslatorRef.Translate(new TransParam(BookRef, false, true));

                             Token.ThrowIfCancellationRequested();

                             AddTranslated(BookRef);
                         }), null)))
                    {
                        if (GetCount() > ThrottleLimit)
                            Thread.Sleep(TrdDelayMs);
                    }
                }

                BookForDone = true;
                WaitAllDone(() => BookForDone);

                this.ProcStage = 6;

                this.Content.SyncSameItemsFromTranslated();

                for (int I = 0; I < this.Content.SameItems.Count; I++)
                {
                    for (int Ir = 0; Ir < this.Content.SameItems[I].Units.Count; Ir++)
                    {
                        var GetUnit = this.Content.SameItems[I].Units[Ir];
                        var Link = TranslatorRef.GetLink();

                        if (GetUnit.Translated.Length > 0)
                        {
                            lock (UnitsReadLock)
                            {
                                TranslatedCount++;
                            }

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

                        if (DequeueCache.TryGetValue(GetUnit.Original, out var CacheResult))
                        {
                            lock (UnitsReadLock)
                            {
                                TranslatedCount++;
                            }
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

                WaitAllDone();

                DequeueCache.Clear();

                this.IsWork = false;
                this.ProcStage = 10;
                TransMainTrd = null;
            });

            TransMainTrd.Start();
        }

        public void Cancel()
        {
            if (TransMainTrd != null)
            {
                try { TransMainTrd.Abort(); }
                catch { }
                TransMainTrd = null;
            }

            if (TrdPool != null)
                TrdPool.CloseAll();

            IsWork = false;
        }

        public void Keep()
        {
            IsStop = false;
            TrdPool.SuspendAll(false);
        }

        public void Stop()
        {
            IsStop = true;
            TrdPool.SuspendAll(true);
        }

        public int TranslatedCount = 0;

        private void AddTranslated(UnitGroup Item)
        {
            lock (UnitsReadLock)
            {
                TranslatedCount += Item.Units.Count;
            }

            if (!Item.ApplyStateChange(UnitTranslationState.Queued).CanDo(-1))
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
                lock (UnitsReadLock)
                {
                    if (TranslatedQueue.Count > 0)
                    {
                        var State = TranslatedQueue.TryDequeue(out BaseUnit Item);

                        IsEnd = false;

                        if (State)
                        {
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
            }
            catch
            {
                IsEnd = false;
                return null;
            }
        }

        private void Clear()
        {
            TrdPool?.CloseAll();
            DequeueCache.Clear();
            ProcStage = 0;
            IsStop = false;
            UnitForDone = false;
            BookForDone = false;

            IsWork = false;
            while (TranslatedQueue.TryDequeue(out _)) { }

            MarkLeadersPercent = 0;

            Cancel();
            this.Content?.Clear();
            this.TranslatedCount = 0;
        }

        public void Close()
        {
            Clear();
        }
    }
}