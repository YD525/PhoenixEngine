using System;
using System.Collections.Generic;
using System.Threading;

namespace PhoenixEngine.PThread
{
    public enum WorkState
    {
        Null = 0, WaitToCreated = 1, Working = 2, WorkEnd = 3
    }
    public class P_ThreadPool<T> where T : class
    {
        private List<P_Thread<T>> Threads = new List<P_Thread<T>>();
        public int ConcurrencyLimit = 0;
        public object SyncLock = new object();

        private bool CanPut = true;
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
                List<P_Thread<T>> WaitDeletes = new List<P_Thread<T>>();

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
        public bool Put(P_Thread<T> ThreadRef, bool Run = true)
        {
            if (!CanPut)
            {
                return false;
            }

            lock (SyncLock)
            {
                if ((ConcurrencyLimit - 1) < this.Threads.Count)
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

                if (Check)
                {
                    CanPut = false;
                }
                else
                {
                    CanPut = true;
                }
            }
        }
    }
    public class P_Thread<T>
    where T : class
    {
        public int ID = 0;
        public WorkState State = WorkState.Null;
        private Action<T> JobFunc;
        private T DataRef;
        private Action<T> OnDestroyedFunc;

        public P_ThreadPool<T> ThreadPoolRef = null;

        public bool SuspendTrd = false;

        private Thread CurrentTrd = null;

        public void GenThread()
        {
            if (CurrentTrd == null)
            {
                CurrentTrd = new Thread(() =>
                {
                    State = WorkState.Working;
                    JobFunc?.Invoke(this.DataRef);
                    while (this.SuspendTrd)
                    {
                        Thread.Sleep(500);
                    }
                    OnDestroyedFunc?.Invoke(this.DataRef);
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
        public P_Thread(P_ThreadPool<T> ThreadPoolRef = null)
        {
            this.ThreadPoolRef = ThreadPoolRef;

            State = WorkState.WaitToCreated;
            GenThread();
        }
        public void SetData(T DataRef)
        { 
            this.DataRef = DataRef;
        }
        public void SetFunc(Action<T> SetJob)
        {
            this.JobFunc = SetJob;
        }
        public void RegDestroyed(Action<T> EndCall)
        {
            this.OnDestroyedFunc = EndCall;
        }
        public bool Start(bool IsBackground = false)
        {
            return Start(DataRef, IsBackground);
        }
        public bool Start(T DataRef,bool IsBackground = false)
        {
            GenThread();

            if (this.State != WorkState.Working)
            {
                this.DataRef = DataRef;
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
