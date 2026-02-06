using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.EngineManagement.Engine;
using PhoenixEngine.EngineManagement.Sequence;
using PhoenixEngine.EngineManagement.Unit;
using PhoenixEngine.TranslateManagement;

namespace PhoenixEngine.TranslateManage
{
    public class BatchTranslation
    {
        public readonly object UnitsTranslatedLocker = new object();

        public ProcContent Content = null;

        public ConcurrentQueue<UnitGroup> UnitsTranslated = new ConcurrentQueue<UnitGroup>();

        public int AutoThreadLimit = 0;

        public bool IsStop = false;

        public bool SkipWordAnalysis = false;

        public Translator TranslatorRef = null;
        public BatchTranslation(Translator SetTranslator,List<BaseUnit> BaseUnits,AggregationMode SetMode, bool ClearCache = false)
        {
            this.TranslatorRef = SetTranslator;

            if (ClearCache)
            {
                TranslatorRef.ClearCache();
            }

            UnionArray SetData = new UnionArray();
            SetData.Load(BaseUnits, TranslatorRef.From);
            Content = ProcContent.Build(TranslatorRef,SetData,SetMode);

            Init();
        }

        public ThreadUsageInfo ThreadUsage = new ThreadUsageInfo();

        public readonly object TranslatedAddLocker = new object();

        private void AddTranslated(UnitGroup Item)
        {
            Item.UPDateLink(this.TranslatorRef);
            Item.UPDateCloudData(this.TranslatorRef);
            UnitsTranslated.Enqueue(Item);
        }

        public object WaitTranslateLock = new object();

        public int GetWorkCount()
        {
            int WorkCount = 0;

            for (int i = 0; i < UnitsToTranslate.Count; i++)
            {
                if (UnitsToTranslate[i].Processing)
                {
                    WorkCount++;
                }
            }

            foreach (var kvp in UnitsLeaderToTranslate)
            {
                if (kvp.Value.Processing)
                {
                    WorkCount++;
                }
            }

            return WorkCount;
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

        public TranslationUnit GetWaitTransUnit(ref List<TranslationUnit> Arrays)
        {
            lock (WaitTranslateLock)
            {
                return Arrays.FirstOrDefault(Unit => Unit.WorkEnd <= 0);
            }
        }

        public TranslationUnit GetWaitTransUnitFromDict(Dictionary<string, TranslationUnit> Dict)
        {
            lock (WaitTranslateLock)
            {
                foreach (var KV in Dict)
                {
                    if (KV.Value.WorkEnd <= 0)
                    {
                        return KV.Value;
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
                UnitsLeaderToTranslate[Key].Processing = false;
                UnitsLeaderToTranslate[Key].WorkEnd = 0;
                UnitsLeaderToTranslate[Key].Translated = false;
                UnitsToTranslate[i].IsDuplicateSource = false;
            }

            for (int i = 0; i < UnitsToTranslate.Count; i++)
            {
                UnitsToTranslate[i].TransText = string.Empty;
                UnitsToTranslate[i].Processing = false;
                UnitsToTranslate[i].WorkEnd = 0;
                UnitsToTranslate[i].Translated = false;
                UnitsToTranslate[i].IsDuplicateSource = false;
            }
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
                if (UnitsToTranslate[i].Processing)
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
                if (Kvp.Value.Processing)
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
