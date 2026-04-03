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

        public bool Init(List<BaseUnit> BaseUnits, AggregationMode SetMode)
        {
            if (ProcStage == 0)
            {
                Clear();
                this.TranslatorRef.SyncTranslatedCount();
                UnionArray SetData = new UnionArray();
                ProcStage = 1;
                SetData.Load(BaseUnits, TranslatorRef.From, ref MarkLeadersPercent);
                Content = ProcContent.Build(TranslatorRef, SetData, SetMode);

                if (SetMode == AggregationMode.Aggregation)
                {
                   ProcContent.ArrangeForParallel(Content,Phoenix.Config.MaxThreadCount);
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

        private P_Thread<T> CreatePhoenixThread<T>(P_ThreadPool<T> PoolRef, T DataRef, Action<T> Job, Action<T> Destroyed) where T : class
        {
            P_Thread<T> CreateTrd = new P_Thread<T>(PoolRef);
            CreateTrd.SetFunc(Job);
            CreateTrd.RegDestroyed(Destroyed);
            CreateTrd.SetData(DataRef);
            return CreateTrd;
        }

        private void WaitAllDone()
        {
            int EmptyConfirmCount = 0;

            while (true)
            {
                bool PoolEmpty = GetWorkingThreadCount() == 0;
                bool QueueEmpty = TranslatedQueue.IsEmpty;

                if (PoolEmpty && QueueEmpty)
                {
                    EmptyConfirmCount++;
                    if (EmptyConfirmCount >= 2)
                        break;
                }
                else
                {
                    EmptyConfirmCount = 0;
                }

                Thread.Sleep(50);
            }
        }

        public void Start()
        {
            int TrdDelayMs = Phoenix.Config.ThrottleDelayMs;

            TranslatedCount = TranslatorRef.CalcTranslatedCount();

            if (TrdPool == null)
            {
                TrdPool = new P_ThreadPool<UnitGroup>();
                TrdPool.ConcurrencyLimit = Phoenix.Config.MaxThreadCount;
            }

            double ThrottleLimit = ((double)TrdPool.ConcurrencyLimit * (double)Phoenix.Config.ThrottleRatio);

            Action<UnitGroup> WorkEndCall = new Action<UnitGroup>((Item) =>
            {
                AddTranslated(Item);
            });

            Action<UnitGroup> NormalCall = new Action<UnitGroup>((ItemRef) =>
            {
                ItemRef = TranslatorRef.Translate(new TransParam(ItemRef, false, true));
            });

            Action<UnitGroup> BookCall = new Action<UnitGroup>((ItemRef) =>
            {
                ItemRef = TranslatorRef.Translate(new TransParam(ItemRef, true, true));
                Thread.Sleep(100);
            });

            TransMainTrd = new Thread(() =>
            {
                this.IsWork = true;
                this.ProcStage = 3;

                for (int i = 0; i < this.Content.Units.Count; i++)
                {
                    UnitGroup GetUnit = this.Content.Units[i];

                    if (!GetUnit.ApplyStateChange(UnitTranslationState.Created).CanDo(-1))
                        continue;

                    while (!TrdPool.Put(CreatePhoenixThread<UnitGroup>(TrdPool, GetUnit, NormalCall, WorkEndCall)))
                    {
                        if (GetCount() > ThrottleLimit)
                            Thread.Sleep(TrdDelayMs);
                    }
                }

                WaitAllDone();

                this.ProcStage = 5;

                for (int i = 0; i < this.Content.Books.Count; i++)
                {
                    UnitGroup GetBook = this.Content.Books[i];

                    if (!GetBook.ApplyStateChange(UnitTranslationState.Created).CanDo(-1))
                        continue;

                    while (!TrdPool.Put(CreatePhoenixThread<UnitGroup>(TrdPool, GetBook, BookCall, WorkEndCall)))
                    {
                        if (GetCount() > ThrottleLimit)
                            Thread.Sleep(TrdDelayMs);
                    }
                }

                WaitAllDone();

                this.ProcStage = 6;

                this.Content.SyncSameItemsFromTranslated();

                for (int i = 0; i < this.Content.SameItems.Count; i++)
                {
                    for (int ir = 0; ir < this.Content.SameItems[i].Units.Count; ir++)
                    {
                        var GetUnit = this.Content.SameItems[i].Units[ir];
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

            for (int i = 0; i < Item.Units.Count; i++)
            {
                TranslatedQueue.Enqueue(Item.Units[i]);
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