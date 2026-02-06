using System;
using System.Collections.Generic;
using System.Threading;

namespace PhoenixEngine.EngineManagement.EThread
{
    public enum WorkState
    {
        Null = 0, WaitToCreated = 1, Working = 2, WorkEnd = 3
    }
    public class PhoenixThreadPool<T1, T2>
    where T1 : class
    where T2 : class
    {
        private List<PhoenixThread<T1, T2>> Threads = new List<PhoenixThread<T1, T2>>();
        public int ConcurrencyLimit = 0;
        public object SyncLock = new object();
        public int GetWorkingThreadCount()
        {
            lock (SyncLock)
            {
                NextTry:
                try
                {
                    int WorkCount = 0;
                    for (int i = 0; i < Threads.Count; i++)
                    {
                        if (Threads[i].State == WorkState.Working)
                        {
                            WorkCount++;
                        }
                    }
                    return WorkCount;
                }
                catch { goto NextTry; }
            }
        }

        public int GetCount()
        {
            lock (SyncLock)
            {
                return this.Threads.Count;
            }
        }

        public void SyncPool()
        {
            lock (SyncLock)
            {
                List<PhoenixThread<T1, T2>> WaitDeletes = new List<PhoenixThread<T1, T2>>();

                for (int i = 0; i < Threads.Count; i++)
                {
                    if (Threads[i].State == WorkState.WorkEnd)
                    {
                        WaitDeletes.Add(Threads[i]);
                    }
                }

                foreach (var GetTrd in WaitDeletes)
                {
                    Threads.Remove(GetTrd);
                }
            }
        }
        public void DeleteTrdByID(int ID)
        {
            lock (SyncLock)
            {
                for (int i = 0; i < Threads.Count; i++)
                {
                    if (Threads[i].ID == ID)
                    {
                        Threads.RemoveAt(i);
                        return;
                    }
                }
            }
        }
        public int GenID()
        {
            lock (SyncLock)
            {
                return this.Threads.Count + 1;
            }
        }
        public bool Put(PhoenixThread<T1, T2> ThreadRef, bool Run = true)
        {
            lock (SyncLock)
            {
                if (ConcurrencyLimit < GetWorkingThreadCount())
                {
                    return false;
                }

                Threads.Add(ThreadRef);

                if (Run)
                {
                    ThreadRef.Start();
                }

                return true;
            }
        }
        public void CloseAll()
        {
            lock (SyncLock)
            {
                while (Threads.Count > 0)
                {
                    try
                    {
                        Threads[0].Close(true);
                        Threads.RemoveAt(0);
                    }
                    catch { }
                }
            }
        }

        public void SuspendAll(bool Check)
        {
            lock (SyncLock)
            {
                for (int i = 0; i < Threads.Count; i++)
                {
                    Threads[i].Suspend(Check);
                }
            }
        }
    }
    public class PhoenixThread<T1, T2>
    where T1 : class
    where T2 : class
    {
        public int ID = 0;
        public WorkState State = WorkState.Null;
        public Action<T1> JobFunc;
        public Action<T2> OnDestroyedFunc;

        public T1 JobParam;
        public T2 DestroyedParam;
        public PhoenixThreadPool<T1, T2> ThreadPoolRef = null;

        public bool SuspendTrd = false;

        private Thread CurrentTrd = null;

        public void GenThread()
        {
            if (CurrentTrd == null)
            {
                CurrentTrd = new Thread(() =>
                {
                    State = WorkState.Working;
                    JobFunc?.Invoke(JobParam);
                    while (this.SuspendTrd)
                    {
                        Thread.Sleep(500);
                    }
                    OnDestroyedFunc?.Invoke(DestroyedParam);
                    State = WorkState.WorkEnd;

                    if (this.ThreadPoolRef != null)
                    {
                        this.ThreadPoolRef.DeleteTrdByID(this.ID);
                    }

                    CurrentTrd = null;
                });

                if (this.ThreadPoolRef != null)
                {
                    this.ID = this.ThreadPoolRef.GenID();
                }
                else
                {
                    this.ID = Guid.NewGuid().GetHashCode();
                }
            }
        }
        public PhoenixThread(PhoenixThreadPool<T1, T2> ThreadPoolRef = null)
        {
            this.ThreadPoolRef = ThreadPoolRef;

            State = WorkState.WaitToCreated;
            GenThread();
        }
        public void SetParam(T1 SetJob, T2 SetDestroyed)
        {
            this.JobParam = SetJob;
            this.DestroyedParam = SetDestroyed;
        }
        public bool Start(bool IsBackground = false)
        {
            GenThread();

            if (this.State != WorkState.Working)
            {
                CurrentTrd.IsBackground = IsBackground;
                CurrentTrd.Start();

                return true;
            }

            return false;
        }

        public bool Suspend(bool Check)
        {
            SuspendTrd = Check;
            return SuspendTrd;
        }

        public void Close(bool System = false)
        {
            if (this.State == WorkState.Working)
            {
                try
                {
                    CurrentTrd.Abort();
                    CurrentTrd = null;
                }
                catch { }
            }

            if (!System)
            {
                if (this.ThreadPoolRef != null)
                {
                    this.ThreadPoolRef.DeleteTrdByID(this.ID);
                }
            }
        }
    }
}
