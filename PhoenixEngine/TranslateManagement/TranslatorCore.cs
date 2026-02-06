using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Threading;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.EngineManagement.Engine;
using PhoenixEngine.EngineManagement.EThread;
using PhoenixEngine.EngineManagement.Sequence;
using PhoenixEngine.EngineManagement.Unit;
using PhoenixEngine.TranslateManagement;

namespace PhoenixEngine.TranslateManage
{
    public class TranslatorCore
    {
        public readonly object UnitsReadLock = new object();

        public ProcContent Content = null;

        public ConcurrentQueue<UnitGroup> PendingTranslationQueue = new ConcurrentQueue<UnitGroup>();
        public ConcurrentQueue<UnitGroup> TranslatedQueue = new ConcurrentQueue<UnitGroup>();

        public int AutoThreadLimit = 0;

        public bool IsStop = false;

        public bool SkipWordAnalysis = false;

        public Translator TranslatorRef = null;

        public PhoenixThreadPool<UnitGroup> TrdPool = null;

        public int ProcStage = 0;
        public TranslatorCore(Translator SetTranslator,List<BaseUnit> BaseUnits,AggregationMode SetMode, bool ClearCache = false)
        {
            ProcStage = 0;
            this.TranslatorRef = SetTranslator;

            if (ClearCache)
            {
                TranslatorRef.ClearCache();
            }

            UnionArray SetData = new UnionArray();
            ProcStage = 1;
            SetData.Load(BaseUnits, TranslatorRef.From);
            ProcStage = 2;
            Content = ProcContent.Build(TranslatorRef,SetData,SetMode);

            Init();
        }

        public ThreadUsageInfo ThreadUsage = new ThreadUsageInfo();

        public readonly object TranslatedAddLocker = new object();

        public object WaitTranslateLock = new object();

        public int GetCount()
        {
            return Content.GetCount();
        }

        public int GetWorkCount()
        {
           return TrdPool.GetWorkingThreadCount();
        }

        public void Init()
        {
            WorkState = 0;

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

        public int AddPendingUnit(UnitGroup Item)
        {
            lock (UnitsReadLock)
            {
                PendingTranslationQueue.Enqueue(Item);
                return PendingTranslationQueue.Count;
            }
        }

        private PhoenixThread<T> CreatePhoenixThread<T>(T DataRef,Action<T> Job,Action<T> Destroyed) where T : class
        {
            PhoenixThread<T> CreateTrd = new PhoenixThread<T>();
            CreateTrd.SetFunc(Job);
            CreateTrd.RegDestroyed(Destroyed);
            CreateTrd.SetData(DataRef);
            return CreateTrd;
        }

        public void Start()
        {
            //The method pointer is invoked after the translation is complete.
            Action<UnitGroup> WorkEndCall = new Action<UnitGroup>((Item) =>
            {
                AddTranslated(Item);
            });

            //Normal type translation calls pointers.
            Action<UnitGroup> NormalCall = new Action<UnitGroup>((Item) =>
            {
                TranslatorRef.Translate(new TransParam(Item,false,true),false);
            });

            //Special type translation calls pointers.
            Action<UnitGroup> BookCall = new Action<UnitGroup>((Item) =>
            {
                TranslatorRef.Translate(new TransParam(Item, true, true), false);
            });



            //if (IsWork || TransMainTrd == null)
            //{
            //    ExitAny = false;
            //    TransMainTrd = new Thread(() =>
            //    {
            //        IsWork = true;

            //        if (ExitAny)
            //        {
            //            SetEndState();
            //            return;
            //        }

            //        TransMainTrdCancel = new CancellationTokenSource();
            //        var Token = TransMainTrdCancel.Token;

            //        int CurrentTrds = 0;

            //        bool IsLeader = true;

            //        WorkState = 2;

            //        while (true)
            //        {
            //            if (!IsStop)
            //            {
            //                try
            //                {
            //                    NextFind:

            //                    ThreadUsage.CurrentThreads = CurrentTrds;
            //                    ThreadUsage.MaxThreads = Phoenix.Config.MaxThreadCount;

            //                    bool CanExit = true;
            //                    Token.ThrowIfCancellationRequested();
            //                    CurrentTrds = GetWorkCount();

            //                    int AutoTrd = Phoenix.Config.MaxThreadCount;

            //                    if (IsLeader)
            //                    {
            //                        if (AutoLeaderTrd <= 0)
            //                        {
            //                            AutoLeaderTrd = 1;
            //                        }
            //                        AutoTrd = AutoLeaderTrd;
            //                    }

            //                    if (CurrentTrds < AutoTrd)
            //                    {
            //                        TranslationUnit Leader = GetWaitTransUnitFromDict(UnitsLeaderToTranslate);
            //                        if (Leader != null)
            //                        {
            //                            Leader.StartWork(this);
            //                            CanExit = false;
            //                            IsLeader = true;
            //                            goto Next;
            //                        }

            //                        TranslationUnit Normal = GetWaitTransUnit(ref UnitsToTranslate);
            //                        if (Normal != null)
            //                        {
            //                            Normal.StartWork(this);
            //                            CanExit = false;
            //                            IsLeader = false;
            //                            goto Next;
            //                        }

            //                        Next:

            //                        if (CurrentTrds > Phoenix.Config.MaxThreadCount * Phoenix.Config.ThrottleRatio)
            //                        {
            //                            AutoSleep = Phoenix.Config.ThrottleDelayMs;
            //                        }
            //                        else
            //                        {
            //                            AutoSleep = 0;
            //                        }

            //                        if (AutoSleep > 0)
            //                        {
            //                            Thread.Sleep(AutoSleep);
            //                        }
            //                    }

            //                    if (CanExit)
            //                    {
            //                        int SucessCount = 0;

            //                        for (int i = 0; i < UnitsToTranslate.Count; i++)
            //                        {
            //                            if (UnitsToTranslate[i].WorkEnd == 2)
            //                            {
            //                                SucessCount++;
            //                            }
            //                        }

            //                        foreach (var kvp in UnitsLeaderToTranslate)
            //                        {
            //                            if (kvp.Value.WorkEnd == 2)
            //                            {
            //                                SucessCount++;
            //                            }
            //                        }

            //                        if (SucessCount == (UnitsToTranslate.Count + UnitsLeaderToTranslate.Count))
            //                        {
            //                            if (SameItems != null)
            //                            {
            //                                if (SameItems.Count > 0)
            //                                {
            //                                    for (int i = 0; i < SameItems.Count; i++)
            //                                    {
            //                                        string GetKey = SameItems.ElementAt(i).Key;
            //                                        SetDuplicateSource(GetKey);
            //                                    }
            //                                }
            //                            }

            //                            IsWork = false;

            //                            WorkState = 3;

            //                            Close();

            //                            return;
            //                        }
            //                        else
            //                        {
            //                            Thread.Sleep(1);
            //                            goto NextFind;
            //                        }
            //                    }
            //                }
            //                catch (OperationCanceledException)
            //                {
            //                    IsWork = false;
            //                    TransMainTrd = null;

            //                    try
            //                    {
            //                        WorkState = -1;
            //                    }
            //                    catch { }
            //                    return;
            //                }
            //            }
            //            else
            //            {
            //                Thread.Sleep(500);
            //            }
            //            Thread.Sleep(1);
            //        }

            //    });

            //    TransMainTrd.Start();
            //}
        }

        public bool ExitAny = false;

      
        public void Cancel()
        {
            TrdPool.CloseAll();
        }
        public void Keep()
        {
            TrdPool.SuspendAll(false);
        }
        public void Stop()
        {
            TrdPool.SuspendAll(true);
        }

        private void AddTranslated(UnitGroup Item)
        {
            lock (UnitsReadLock)
            {
                Item.UPDateLink(this.TranslatorRef);
            }
                
            TranslatedQueue.Enqueue(Item);
        }
        public UnitGroup DequeueTranslated(out bool IsEnd)
        {
            try
            {
                lock (UnitsReadLock)
                {
                    if (TranslatedQueue.Count > 0)
                    {
                        var State = TranslatedQueue.TryDequeue(out UnitGroup Item);

                        IsEnd = false;

                        if (State)
                        {
                            return Item;

                        }
                        else
                        {
                            return null;
                        }
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
