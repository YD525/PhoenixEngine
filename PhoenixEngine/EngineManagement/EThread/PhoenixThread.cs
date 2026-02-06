using System;
using System.Collections.Generic;
using System.Threading;

namespace PhoenixEngine.EngineManagement.EThread
{
    public enum WorkState
    { 
       Null=0,WaitToCreated = 1,Working = 2,WorkEnd = 3
    }
    public class PhoenixThreadPool<T> where T : class
    {
        public List<PhoenixThread<T>> Threads = new List<PhoenixThread<T>>();

        public int GetWorkCount()
        {
            return 0;
        }
    }
    public class PhoenixThread<T> where T : class
    {
        public int ID = 0;
        public WorkState State = WorkState.Null;
        public Action<T> Job;
        public Action<T> OnDestroyed;

        public T JobFunc;
        public T DestroyedFunc;

        public bool SuspendTrd = false;

        private Thread CurrentTrd = null;

        public void GenThread()
        {
            if (CurrentTrd == null)
            {
                CurrentTrd = new Thread(() =>
                {
                    State = WorkState.Working;
                    Job.Invoke(JobFunc);
                    while (this.SuspendTrd)
                    {
                        Thread.Sleep(500);
                    }
                    OnDestroyed.Invoke(DestroyedFunc);
                    State = WorkState.WorkEnd;
                    CurrentTrd = null;
                });
            }
        }
        public PhoenixThread()
        {
            State = WorkState.WaitToCreated;

            GenThread();
        }
        public bool Start(T Item,bool IsBackground = false)
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

        public void Close()
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
        }
    }
}
