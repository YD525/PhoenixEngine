using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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

        public ConcurrentQueue<UnitGroup> TranslatedQueue = new ConcurrentQueue<UnitGroup>();

        public int AutoThreadLimit = 0;

        public bool IsStop = false;

        public bool SkipWordAnalysis = false;

        public Translator TranslatorRef = null;

        public PhoenixThreadPool<UnitGroup> TrdPool = null;

        public int ProcStage = 0;

        public bool IsWork = false;
        public TranslatorCore(Translator SetTranslator,bool ClearCache = false)
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
                return TrdPool.GetWorkingThreadCount();
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

        public int GetWorkCount()
        {
           return TrdPool.GetWorkingThreadCount();
        }

        public bool Init(List<BaseUnit> BaseUnits, AggregationMode SetMode)
        {
            if (ProcStage == 0)
            {
                UnionArray SetData = new UnionArray();
                ProcStage = 1;
                SetData.Load(BaseUnits, TranslatorRef.From,ref MarkLeadersPercent);
                Content = ProcContent.Build(TranslatorRef, SetData, SetMode);
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

        private PhoenixThread<T> CreatePhoenixThread<T>(PhoenixThreadPool<T> PoolRef,T DataRef,Action<T> Job,Action<T> Destroyed) where T : class
        {
            PhoenixThread<T> CreateTrd = new PhoenixThread<T>(PoolRef);
            CreateTrd.SetFunc(Job);
            CreateTrd.RegDestroyed(Destroyed);
            CreateTrd.SetData(DataRef);
            return CreateTrd;
        }

        public void Start()
        {
            TranslatedCount = Phoenix.GetTranslatedCount(Phoenix.GetFileUniqueKey());

            if (TrdPool == null)
            {
                TrdPool = new PhoenixThreadPool<UnitGroup>();
                TrdPool.ConcurrencyLimit = Phoenix.Config.MaxThreadCount;
            }
            //The method pointer is invoked after the translation is complete.
            Action<UnitGroup> WorkEndCall = new Action<UnitGroup>((Item) =>
            {
                AddTranslated(Item);
            });

            //Normal type translation calls pointers.
            Action<UnitGroup> NormalCall = new Action<UnitGroup>((ItemRef) =>
            {
                ItemRef = TranslatorRef.Translate(new TransParam(ItemRef,false,true));
            });

            //Special type translation calls pointers.
            Action<UnitGroup> BookCall = new Action<UnitGroup>((ItemRef) =>
            {
                ItemRef = TranslatorRef.Translate(new TransParam(ItemRef,true, true));
            });

            TransMainTrd = new Thread(() => 
            {
                this.IsWork = true;
                this.ProcStage = 3;
                //First, translate the traditional type.
                for (int i = 0; i < this.Content.Units.Count; i++)
                {
                    UnitGroup GetPointer = this.Content.Units[i];
                    while (!TrdPool.Put(CreatePhoenixThread<UnitGroup>(TrdPool,GetPointer, NormalCall, WorkEndCall)))
                    {
                        Thread.Sleep(100);
                    }
                }

                this.ProcStage = 5;
                //Book translation will be done last.
                for (int i = 0; i < this.Content.Books.Count; i++)
                {
                    UnitGroup GetBookPointer = this.Content.Books[i];
                    while (!TrdPool.Put(CreatePhoenixThread<UnitGroup>(TrdPool,GetBookPointer, BookCall, WorkEndCall)))
                    {
                        Thread.Sleep(100);
                    }
                }

                this.ProcStage = 6;
                //Processing the same object.
                for (int i = 0; i < this.Content.SameItems.Count; i++)
                {
                    for (int ir = 0; ir < this.Content.SameItems[i].Units.Count; ir++)
                    {
                        this.Content.SameItems[i].Units[ir].Translated = this.Content.SearchTranslated(this.Content.SameItems[i].Units[ir].Original);
                    }
                }

                this.IsWork = false;
                this.ProcStage = 10;
                TransMainTrd = null;
            });

            TransMainTrd.Start();
        }

        public bool ExitAny = false;

        public void ReSet()
        {
            lock (UnitsReadLock)
            {
                for (int i = 0; i < this.Content.UnionData.Units.Count; i++)
                {
                    this.Content.UnionData.Units[i].ReSet();
                }
                for (int i = 0; i < this.Content.UnionData.Leaders.Count; i++)
                {
                    var GetKey = this.Content.UnionData.Leaders.ElementAt(i).Key;
                    this.Content.UnionData.Leaders[GetKey].ReSet();
                }
            }
        }
      
        public void Cancel()
        {
            if (TransMainTrd != null)
            {
                try
                {
                    TransMainTrd.Abort();
                }
                catch { }

                TransMainTrd = null;
            }

            if (TrdPool != null)
            {
                TrdPool.CloseAll();
            }

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
                Item.UPDateLink(this.TranslatorRef);
                TranslatedCount += Item.Units.Count;
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

                    bool NoMoreWork = (this.ProcStage == 10 && GetWorkCount() == 0);

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

        public void Clear()
        {
            ProcStage = 0;
            IsStop = false;

            Cancel();
            this.Content?.Clear();
            this.TranslatedCount = Phoenix.GetTranslatedCount(Phoenix.GetFileUniqueKey());

            GC.Collect();
        }

        public void Close()
        {
            this.TranslatedCount = 0;
        }
    }
}
